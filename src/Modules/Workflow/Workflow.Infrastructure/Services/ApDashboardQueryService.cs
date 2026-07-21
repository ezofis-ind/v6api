using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application;
using SaaSApp.Workflow.Application.Contracts;
using SaaSApp.Workflow.Domain.Enums;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// AP Command Center dashboard: aggregates invoice rows from workflow.agentDataValidation_* and ezfb form tables.
/// </summary>
public sealed class ApDashboardQueryService : IApDashboardQueryService
{
  private readonly ITenantContext _tenantContext;
  private readonly IWorkflowRepository _workflowRepository;
  private readonly IWorkflowEzfbFormDataLoader _formDataLoader;
  private readonly ILogger<ApDashboardQueryService> _logger;

  public ApDashboardQueryService(
    ITenantContext tenantContext,
    IWorkflowRepository workflowRepository,
    IWorkflowEzfbFormDataLoader formDataLoader,
    ILogger<ApDashboardQueryService> logger)
  {
    _tenantContext = tenantContext;
    _workflowRepository = workflowRepository;
    _formDataLoader = formDataLoader;
    _logger = logger;
  }

  public async Task<ApDashboardResult> GetDashboardAsync(
    Guid tenantId,
    ApDashboardRequest request,
    CancellationToken cancellationToken = default)
  {
    var connectionString = _tenantContext.ConnectionString
      ?? throw new InvalidOperationException("Tenant connection string not resolved.");

    var (rangeStart, rangeEnd) = ResolveRange(request);
    var (prevStart, prevEnd) = ResolvePreviousRange(request.Period, rangeStart, rangeEnd);

    var workflows = await _workflowRepository.ListAsync(cancellationToken);
    var workflowBySuffix = workflows.ToDictionary(
      w => w.Id.ToString("N")[..8],
      w => w,
      StringComparer.OrdinalIgnoreCase);

    var allInvoices = new List<ApDashboardInvoiceDto>();
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);

    var agentTables = await DiscoverAgentValidationTablesAsync(connection, cancellationToken);
    if (agentTables.Count == 0)
    {
      _logger.LogWarning(
        "AP dashboard: no workflow.agentDataValidation_* tables found for tenant {TenantId}.",
        tenantId);
    }

    foreach (var agentTable in agentTables)
    {
      var suffix = AgentTableSuffix(agentTable);
      if (request.WorkflowId is Guid wfId
          && !string.Equals(suffix, wfId.ToString("N")[..8], StringComparison.OrdinalIgnoreCase))
        continue;

      workflowBySuffix.TryGetValue(suffix, out var workflow);
      var workflowId = workflow?.Id ?? request.WorkflowId ?? Guid.Empty;
      if (workflowId == Guid.Empty)
      {
        workflowId = workflows.FirstOrDefault(w =>
            w.Id.ToString("N").StartsWith(suffix, StringComparison.OrdinalIgnoreCase))?.Id
          ?? Guid.Empty;
      }

      var workflowName = workflow?.Name ?? $"AP Workflow ({suffix})";

      try
      {
        var rows = await LoadAgentTableInvoicesAsync(
          connection,
          agentTable,
          suffix,
          workflowId,
          workflowName,
          cancellationToken);
        _logger.LogInformation(
          "AP dashboard loaded {RowCount} invoice row(s) from workflow.{AgentTable}.",
          rows.Count,
          agentTable);
        allInvoices.AddRange(rows);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "AP dashboard skipped agent table {AgentTable}.", agentTable);
      }
    }

    if (allInvoices.Count == 0)
      _logger.LogWarning(
        "AP dashboard loaded 0 invoices for tenant {TenantId} from {AgentTableCount} agent table(s).",
        tenantId,
        agentTables.Count);

    // Build dropdown options from the selected period (before extra filters),
    // so UI still shows available departments/suppliers for the day/month window.
    var inPeriod = allInvoices
      .Where(i => InRange(ResolvePeriodDate(i, request.Period), rangeStart, rangeEnd))
      .ToList();
    var filterOptions = ApDashboardFilterSupport.BuildFilterOptions(inPeriod);
    var current = ApDashboardFilterSupport.ApplyFilters(inPeriod, request);
    var previous = ApDashboardFilterSupport.ApplyFilters(
      allInvoices.Where(i => InRange(ResolvePeriodDate(i, request.Period), prevStart, prevEnd)).ToList(),
      request);

    return ApDashboardBuilder.Build(
      request,
      rangeStart,
      rangeEnd,
      prevStart,
      prevEnd,
      current,
      previous,
      filterOptions);
  }

  private async Task<List<ApDashboardInvoiceDto>> LoadAgentTableInvoicesAsync(
    SqlConnection connection,
    string agentTable,
    string suffix,
    Guid workflowId,
    string workflowName,
    CancellationToken cancellationToken)
  {
    // Core source: latest AgentResponse per instance — no joins required.
    var sql = $"""
SELECT
    a.ProcessId AS InstanceId,
    a.AgentResponse,
    a.CreatedAt AS AgentCreatedAt
FROM (
    SELECT ProcessId, MAX(Id) AS MaxId
    FROM workflow.[{agentTable}]
    WHERE IsDeleted = 0
    GROUP BY ProcessId
) latest
INNER JOIN workflow.[{agentTable}] a ON a.Id = latest.MaxId
WHERE a.IsDeleted = 0;
""";

    var list = new List<RawAgentRow>();
    await using (var cmd = new SqlCommand(sql, connection) { CommandTimeout = 60 })
    {
      await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
      while (await reader.ReadAsync(cancellationToken))
      {
        list.Add(new RawAgentRow(
          reader.GetGuid(0),
          reader.IsDBNull(1) ? null : reader.GetString(1),
          reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2)));
      }
    }

    var invoices = new List<ApDashboardInvoiceDto>(list.Count);
    foreach (var row in list)
    {
      var fields = ParseAgentFields(row.AgentJson);
      ApplyApFieldAliases(fields);

      if (NeedsFormFallback(fields))
      {
        try
        {
          await TryMergeFormFieldsFromProcessFormAsync(
            connection,
            suffix,
            row.InstanceId,
            fields,
            cancellationToken);
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "AP dashboard form fallback skipped for instance {InstanceId}.", row.InstanceId);
        }
      }

      InstanceEnrichment enrichment;
      try
      {
        enrichment = await TryLoadInstanceEnrichmentAsync(
          connection,
          suffix,
          row.InstanceId,
          cancellationToken);
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "AP dashboard enrichment skipped for instance {InstanceId}.", row.InstanceId);
        enrichment = new InstanceEnrichment(null, null, null, null, null, null);
      }

      invoices.Add(MapInvoice(
        workflowId,
        workflowName,
        row.InstanceId,
        enrichment.Reference,
        fields,
        enrichment.InstanceStatus,
        enrichment.Review,
        enrichment.StageType,
        enrichment.StartedAt,
        enrichment.CompletedAt,
        row.AgentCreatedAt));
    }

    return invoices;
  }

  private sealed record RawAgentRow(Guid InstanceId, string? AgentJson, DateTime? AgentCreatedAt);

  private sealed record InstanceEnrichment(
    string? Reference,
    string? InstanceStatus,
    string? Review,
    string? StageType,
    DateTime? StartedAt,
    DateTime? CompletedAt);

  private static bool NeedsFormFallback(IReadOnlyDictionary<string, string> fields) =>
    fields.Count == 0
    || (!fields.ContainsKey("Amount") && !fields.ContainsKey("InvoiceAmount") && !fields.ContainsKey("Total Due"))
    || (!fields.ContainsKey("Supplier") && !fields.ContainsKey("Vendor Name") && !fields.ContainsKey("Supplier Name"));

  private static void ApplyApFieldAliases(Dictionary<string, string> fields)
  {
    CopyAlias(fields, "Supplier Name", "Supplier");
    CopyAlias(fields, "Vendor Name", "Supplier");
    CopyAlias(fields, "Total Due", "Amount");
    CopyAlias(fields, "Invoice Amount", "Amount");
    CopyAlias(fields, "Invoice Date", "DocumentDate");
    CopyAlias(fields, "decision", "MatchedStatus");
  }

  private static void CopyAlias(Dictionary<string, string> fields, string sourceKey, string targetKey)
  {
    if (fields.ContainsKey(targetKey))
      return;
    if (fields.TryGetValue(sourceKey, out var value) && !string.IsNullOrWhiteSpace(value))
      fields[targetKey] = value;
  }

  private async Task TryMergeFormFieldsFromProcessFormAsync(
    SqlConnection connection,
    string suffix,
    Guid instanceId,
    Dictionary<string, string> fields,
    CancellationToken cancellationToken)
  {
    var processFormTable = $"processForm_{suffix}";
    if (!await TableExistsAsync(connection, processFormTable, cancellationToken))
      return;

    var formIdColumn = await ResolveProcessFormIdColumnAsync(connection, processFormTable, cancellationToken);
    var sql = $"""
SELECT TOP 1
    CAST(pf.[{formIdColumn}] AS NVARCHAR(64)) AS FormId,
    pf.FormEntryId
FROM workflow.[{processFormTable}] pf
WHERE pf.IsDeleted = 0 AND pf.WorkflowInstanceId = @InstanceId
ORDER BY pf.CreatedAt DESC, pf.Id DESC;
""";

    await using var cmd = new SqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@InstanceId", instanceId);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
      return;

    var formId = ReadScalarString(reader, 0);
    var formEntryId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
    if (string.IsNullOrWhiteSpace(formId) || formEntryId <= 0)
      return;

    var formJson = await _formDataLoader.LoadFormDataJsonAsync(formId, formEntryId, cancellationToken);
    MergeFormFields(fields, formJson);
    ApplyApFieldAliases(fields);
  }

  private static async Task<InstanceEnrichment> TryLoadInstanceEnrichmentAsync(
    SqlConnection connection,
    string suffix,
    Guid instanceId,
    CancellationToken cancellationToken)
  {
    var instancesTable = $"WorkflowInstances_{suffix}";
    var transactionTable = $"transaction_{suffix}";
    string? reference = null;
    string? instanceStatus = null;
    DateTime? startedAt = null;
    DateTime? completedAt = null;
    string? review = null;
    string? stageType = null;

    if (await TableExistsAsync(connection, instancesTable, cancellationToken))
    {
      var sql = $"""
SELECT TOP 1 i.ReferenceNumber, i.Status, i.StartedAtUtc, i.CompletedAtUtc
FROM workflow.[{instancesTable}] i
WHERE i.Id = @InstanceId;
""";
      await using var cmd = new SqlCommand(sql, connection);
      cmd.Parameters.AddWithValue("@InstanceId", instanceId);
      await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
      if (await reader.ReadAsync(cancellationToken))
      {
        reference = ReadScalarString(reader, 0);
        instanceStatus = MapInstanceStatus(ReadScalarString(reader, 1));
        startedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
        completedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
      }
    }

    if (await TableExistsAsync(connection, transactionTable, cancellationToken))
    {
      var sql = $"""
SELECT TOP 1 t.Review, t.StageType
FROM workflow.[{transactionTable}] t
WHERE t.IsDeleted = 0 AND t.WorkflowInstanceId = @InstanceId
ORDER BY t.CreatedAt DESC, t.Id DESC;
""";
      await using var cmd = new SqlCommand(sql, connection);
      cmd.Parameters.AddWithValue("@InstanceId", instanceId);
      await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
      if (await reader.ReadAsync(cancellationToken))
      {
        review = ReadScalarString(reader, 0);
        stageType = ReadScalarString(reader, 1);
      }
    }

    return new InstanceEnrichment(reference, instanceStatus, review, stageType, startedAt, completedAt);
  }

  private static string? ReadScalarString(SqlDataReader reader, int ordinal)
  {
    if (reader.IsDBNull(ordinal))
      return null;

    return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
  }

  private static string? MapInstanceStatus(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw))
      return null;

    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
        && Enum.IsDefined(typeof(WorkflowInstanceStatus), code))
      return Enum.GetName(typeof(WorkflowInstanceStatus), code);

    return raw;
  }

  private static async Task<IReadOnlyList<string>> DiscoverAgentValidationTablesAsync(
    SqlConnection connection,
    CancellationToken cancellationToken)
  {
    const string sql = """
      SELECT t.name
      FROM sys.tables t
      INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
      WHERE s.name = N'workflow'
        AND t.name LIKE N'agentDataValidation[_]%'
      ORDER BY t.name
      """;
    var tables = new List<string>();
    await using var cmd = new SqlCommand(sql, connection);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
      tables.Add(reader.GetString(0));
    return tables;
  }

  private static string AgentTableSuffix(string tableName)
  {
    const string prefix = "agentDataValidation_";
    return tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
      ? tableName[prefix.Length..]
      : tableName;
  }

  private static Dictionary<string, string> ParseAgentFields(string? agentJson)
  {
    var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(agentJson))
      return fields;

    try
    {
      using var doc = JsonDocument.Parse(agentJson);
      try
      {
        var (parsed, _) = ApAgentMetadataParser.ParseFieldsPayload(doc.RootElement);
        foreach (var (k, v) in parsed)
          fields[k] = v;
      }
      catch (JsonException)
      {
        FlattenScalarJsonProperties(doc.RootElement, fields);
      }

      MergeMatchingAgentDebugFields(doc.RootElement, fields);
      ApplyApFieldAliases(fields);
    }
    catch (JsonException)
    {
      // ignore invalid json
    }

    return fields;
  }

  private static void FlattenScalarJsonProperties(JsonElement root, Dictionary<string, string> fields)
  {
    if (root.ValueKind != JsonValueKind.Object)
      return;

    foreach (var prop in root.EnumerateObject())
    {
      if (prop.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
        continue;
      var val = prop.Value.ToString();
      if (!string.IsNullOrWhiteSpace(val))
        fields[prop.Name] = val;
    }
  }

  private static void MergeFormFields(Dictionary<string, string> target, string? formJson)
  {
    if (string.IsNullOrWhiteSpace(formJson))
      return;

    try
    {
      using var doc = JsonDocument.Parse(formJson);
      if (doc.RootElement.ValueKind != JsonValueKind.Object)
        return;

      foreach (var prop in doc.RootElement.EnumerateObject())
      {
        if (prop.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
          continue;
        var val = prop.Value.ToString();
        if (string.IsNullOrWhiteSpace(val))
          continue;
        if (!target.ContainsKey(prop.Name))
          target[prop.Name] = val;
      }
    }
    catch (JsonException)
    {
      // ignore
    }
  }

  private static ApDashboardInvoiceDto MapInvoice(
    Guid workflowId,
    string workflowName,
    Guid instanceId,
    string? reference,
    IReadOnlyDictionary<string, string> fields,
    string? instanceStatus,
    string? review,
    string? stageType,
    DateTime? startedAt,
    DateTime? completedAt,
    DateTime? agentCreatedAt)
  {
    var supplier = FirstField(fields, "Supplier", "Supplier Name", "Vendor Name", "VendorName", "Vendor");
    var amount = ParseDecimal(FirstField(fields, "Amount", "Invoice Amount", "InvoiceAmount", "Total Amount", "Total Due", "PO Amount"));
    var currency = FirstField(fields, "Currency") ?? "USD";
    var invoiceDate = ParseDate(FirstField(fields, "DocumentDate", "Invoice Date", "InvoiceDate", "PO Date"));
    var dueDate = ParseDate(FirstField(fields, "DueDate", "Due Date", "Payment Due Date"))
      ?? invoiceDate?.AddDays(ParseTermsDays(FirstField(fields, "Terms", "TERMS")));
    var department = FirstField(fields, "Department", "Cost Center", "CostCenter", "GL Category", "GlCategory") ?? "General";
    var matchedStatus = FirstField(fields, "MatchedStatus", "Matched Status", "decision", "AiStatus");
    var country = ResolveCountry(fields);
    var paymentStatus = ResolvePaymentStatus(instanceStatus, review, stageType, dueDate);
    var approvalStatus = ApDashboardFilterSupport.ResolveApprovalStatus(paymentStatus, matchedStatus, review);
    var requestStatus = ApDashboardFilterSupport.ResolveRequestStatus(instanceStatus);
    var processingDays = ComputeProcessingDays(startedAt, completedAt, agentCreatedAt);
    var riskLevel = ResolveInvoiceRiskLevel(paymentStatus, matchedStatus, amount, dueDate);

    return new ApDashboardInvoiceDto(
      workflowId,
      workflowName,
      instanceId,
      reference,
      string.IsNullOrWhiteSpace(supplier) ? "Unknown" : supplier,
      amount,
      currency,
      invoiceDate,
      dueDate,
      department,
      country,
      paymentStatus,
      approvalStatus,
      requestStatus,
      matchedStatus,
      riskLevel,
      agentCreatedAt ?? startedAt,
      processingDays);
  }

  private static string ResolveInvoiceRiskLevel(
    string paymentStatus,
    string? matchedStatus,
    decimal amount,
    DateTime? dueDate)
  {
    var score = 0;
    var matched = matchedStatus ?? string.Empty;

    if (string.Equals(paymentStatus, "overdue", StringComparison.OrdinalIgnoreCase))
      score += 3;
    else if (string.Equals(paymentStatus, "due_today", StringComparison.OrdinalIgnoreCase))
      score += 2;
    else if (string.Equals(paymentStatus, "pending", StringComparison.OrdinalIgnoreCase))
      score += 1;

    if (matched.Contains("reject", StringComparison.OrdinalIgnoreCase)
        || matched.Contains("unmatch", StringComparison.OrdinalIgnoreCase)
        || matched.Contains("mismatch", StringComparison.OrdinalIgnoreCase))
      score += 3;
    else if (matched.Contains("partial", StringComparison.OrdinalIgnoreCase))
      score += 2;
    else if (matched.Contains("match", StringComparison.OrdinalIgnoreCase)
             || matched.Contains("approv", StringComparison.OrdinalIgnoreCase))
      score -= 1;

    if (amount >= 100_000m)
      score += 2;
    else if (amount >= 10_000m)
      score += 1;

    if (dueDate.HasValue)
    {
      var daysPastDue = (DateTime.UtcNow.Date - dueDate.Value.Date).TotalDays;
      if (daysPastDue > 30)
        score += 2;
      else if (daysPastDue > 0)
        score += 1;
    }

    if (score >= 4)
      return "high";
    if (score >= 2)
      return "medium";
    return "low";
  }

  private static string ResolvePaymentStatus(
    string? instanceStatus,
    string? review,
    string? stageType,
    DateTime? dueDate)
  {
    var reviewNorm = review?.Trim() ?? string.Empty;
    var stageNorm = stageType?.Trim() ?? string.Empty;
    var statusNorm = instanceStatus?.Trim() ?? string.Empty;

    if (reviewNorm.Contains("paid", StringComparison.OrdinalIgnoreCase)
        || statusNorm.Equals("Completed", StringComparison.OrdinalIgnoreCase)
        && stageNorm.Equals("END", StringComparison.OrdinalIgnoreCase))
      return "paid";

    if (dueDate.HasValue)
    {
      var today = DateTime.UtcNow.Date;
      if (dueDate.Value.Date < today)
        return "overdue";
      if (dueDate.Value.Date == today)
        return "due_today";
    }

    if (reviewNorm.Contains("approv", StringComparison.OrdinalIgnoreCase)
        || reviewNorm.Contains("verified", StringComparison.OrdinalIgnoreCase))
      return "approved";

    return "pending";
  }

  private static decimal? ComputeProcessingDays(
    DateTime? startedAt,
    DateTime? completedAt,
    DateTime? agentCreatedAt)
  {
    var start = startedAt ?? agentCreatedAt;
    var end = completedAt ?? agentCreatedAt;
    if (!start.HasValue || !end.HasValue)
      return null;
    return (decimal)Math.Max(0, (end.Value - start.Value).TotalDays);
  }

  private static string? ResolveCountry(IReadOnlyDictionary<string, string> fields)
  {
    var explicitCountry = FirstField(fields, "Country", "CountryCode", "Supplier Country");
    if (!string.IsNullOrWhiteSpace(explicitCountry))
      return explicitCountry.Trim().ToUpperInvariant();

    var address = FirstField(fields, "SupplierAddress", "Supplier Address", "Vendor Address", "VendorAddress");
    if (string.IsNullOrWhiteSpace(address))
      return "UN";

    var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
      return "UN";

    var last = parts[^1];
    if (last.Length == 2)
      return last.ToUpperInvariant();

    return last switch
    {
      var s when s.Contains("United States", StringComparison.OrdinalIgnoreCase) => "US",
      var s when s.Contains("Germany", StringComparison.OrdinalIgnoreCase) => "DE",
      var s when s.Contains("India", StringComparison.OrdinalIgnoreCase) => "IN",
      var s when s.Contains("United Kingdom", StringComparison.OrdinalIgnoreCase) || s.Equals("UK", StringComparison.OrdinalIgnoreCase) => "GB",
      _ => "UN"
    };
  }

  private static (DateTime Start, DateTime End) ResolveRange(ApDashboardRequest request)
  {
    if (request.Period == ApDashboardPeriod.Custom
        && request.FromUtc.HasValue
        && request.ToUtc.HasValue)
      return (request.FromUtc.Value, request.ToUtc.Value);

    var now = DateTime.UtcNow;
    return request.Period switch
    {
      ApDashboardPeriod.Today => DayRange(now),
      ApDashboardPeriod.Tomorrow => DayRange(now.AddDays(1)),
      ApDashboardPeriod.ThisWeek => WeekRange(now),
      ApDashboardPeriod.LastMonth => MonthRange(now.AddMonths(-1)),
      ApDashboardPeriod.ThisQuarter => QuarterRange(now),
      ApDashboardPeriod.ThisYear => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now),
      _ => MonthRange(now)
    };
  }

  private static (DateTime Start, DateTime End) ResolvePreviousRange(
    ApDashboardPeriod period,
    DateTime currentStart,
    DateTime currentEnd)
  {
    // Today → yesterday; Tomorrow → today; ThisWeek → previous week; otherwise same-length prior window.
    if (period is ApDashboardPeriod.Today or ApDashboardPeriod.Tomorrow)
      return DayRange(currentStart.AddDays(-1));

    if (period == ApDashboardPeriod.ThisWeek)
      return WeekRange(currentStart.AddDays(-7));

    var span = currentEnd - currentStart;
    return (currentStart - span, currentStart);
  }

  private static (DateTime Start, DateTime End) MonthRange(DateTime anchor)
  {
    var start = new DateTime(anchor.Year, anchor.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddMonths(1).AddTicks(-1);
    return (start, end);
  }

  private static (DateTime Start, DateTime End) DayRange(DateTime anchor)
  {
    var start = new DateTime(anchor.Year, anchor.Month, anchor.Day, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddDays(1).AddTicks(-1);
    return (start, end);
  }

  /// <summary>Monday 00:00 UTC through Sunday 23:59:59.999 UTC of the week containing <paramref name="anchor"/>.</summary>
  private static (DateTime Start, DateTime End) WeekRange(DateTime anchor)
  {
    var day = new DateTime(anchor.Year, anchor.Month, anchor.Day, 0, 0, 0, DateTimeKind.Utc);
    // Monday = start of week (ISO-style)
    var diff = ((int)day.DayOfWeek + 6) % 7; // Sunday=6, Monday=0, ...
    var start = day.AddDays(-diff);
    var end = start.AddDays(7).AddTicks(-1);
    return (start, end);
  }

  private static (DateTime Start, DateTime End) QuarterRange(DateTime anchor)
  {
    var quarter = (anchor.Month - 1) / 3;
    var startMonth = quarter * 3 + 1;
    var start = new DateTime(anchor.Year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
    return (start, anchor);
  }

  /// <summary>
  /// Period date for filtering.
  /// Today / Tomorrow / ThisWeek prefer due date (cash / AP calendar); other periods prefer agent CreatedAt.
  /// </summary>
  private static DateTime? ResolvePeriodDate(ApDashboardInvoiceDto invoice, ApDashboardPeriod period) =>
    period is ApDashboardPeriod.Today or ApDashboardPeriod.Tomorrow or ApDashboardPeriod.ThisWeek
      ? invoice.DueDate ?? invoice.CreatedAtUtc ?? invoice.InvoiceDate
      : invoice.CreatedAtUtc ?? invoice.InvoiceDate ?? invoice.DueDate;

  private static bool InRange(DateTime? value, DateTime start, DateTime end)
  {
    if (!value.HasValue)
      return false;

    var normalized = value.Value.Kind == DateTimeKind.Unspecified
      ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
      : value.Value.ToUniversalTime();

    return normalized >= start && normalized <= end;
  }

  private static void MergeMatchingAgentDebugFields(JsonElement root, Dictionary<string, string> fields)
  {
    if (!TryGetPropertyIgnoreCase(root, "debug", out var debug) || debug.ValueKind != JsonValueKind.Object)
      return;

    foreach (var prop in debug.EnumerateObject())
    {
      if (prop.Value.ValueKind != JsonValueKind.Array
          || !prop.Name.Contains("Field Matching", StringComparison.OrdinalIgnoreCase))
        continue;

      foreach (var row in prop.Value.EnumerateArray())
      {
        if (row.ValueKind != JsonValueKind.Object)
          continue;

        var label = ReadJsonString(row, "Field");
        var invoiceValue = ReadJsonString(row, "Invoice Value");
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(invoiceValue))
          continue;

        if (!fields.ContainsKey(label))
          fields[label] = invoiceValue.Trim();
      }
    }
  }

  private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
  {
    foreach (var prop in obj.EnumerateObject())
    {
      if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
        continue;

      value = prop.Value;
      return true;
    }

    value = default;
    return false;
  }

  private static string? ReadJsonString(JsonElement obj, string name)
  {
    if (!TryGetPropertyIgnoreCase(obj, name, out var value))
      return null;

    return value.ValueKind switch
    {
      JsonValueKind.String => value.GetString(),
      JsonValueKind.Number => value.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => null
    };
  }

  private static async Task<string> ResolveProcessFormIdColumnAsync(
    SqlConnection connection,
    string tableName,
    CancellationToken cancellationToken)
  {
    const string sql = """
      SELECT COLUMN_NAME
      FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = N'workflow'
        AND TABLE_NAME = @TableName
        AND COLUMN_NAME IN (N'WFormId', N'FormId')
      """;
    await using var cmd = new SqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@TableName", tableName);

    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
      columns.Add(reader.GetString(0));

    if (columns.Contains("WFormId"))
      return "WFormId";
    if (columns.Contains("FormId"))
      return "FormId";

    return "WFormId";
  }

  private static string? FirstField(IReadOnlyDictionary<string, string> fields, params string[] keys)
  {
    foreach (var key in keys)
    {
      if (fields.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
        return val.Trim();
    }
    return null;
  }

  private static decimal ParseDecimal(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return 0;
    var cleaned = value.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
    return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
  }

  private static DateTime? ParseDate(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;
    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
      return d;
    if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal, out d))
      return d;
    return null;
  }

  private static int ParseTermsDays(string? terms)
  {
    if (string.IsNullOrWhiteSpace(terms))
      return 30;
    var digits = new string(terms.Where(char.IsDigit).ToArray());
    return int.TryParse(digits, out var days) && days > 0 ? days : 30;
  }

  private static async Task<bool> TableExistsAsync(
    SqlConnection connection,
    string tableName,
    CancellationToken cancellationToken)
  {
    const string sql = """
      SELECT 1
      FROM sys.tables t
      INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
      WHERE s.name = N'workflow' AND t.name = @TableName
      """;
    await using var cmd = new SqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("@TableName", tableName);
    var result = await cmd.ExecuteScalarAsync(cancellationToken);
    return result != null && result != DBNull.Value;
  }
}
