using System.Globalization;
using SaaSApp.Workflow.Application.Contracts;

namespace SaaSApp.Workflow.Infrastructure.Services;

internal static class ApDashboardBuilder
{
  public static ApDashboardResult Build(
    ApDashboardRequest request,
    DateTime rangeStartUtc,
    DateTime rangeEndUtc,
    DateTime previousRangeStartUtc,
    DateTime previousRangeEndUtc,
    IReadOnlyList<ApDashboardInvoiceDto> currentInvoices,
    IReadOnlyList<ApDashboardInvoiceDto> previousInvoices,
    ApDashboardFilterOptionsDto filterOptions)
  {
    var periodLabel = BuildPeriodLabel(request.Period, rangeStartUtc, rangeEndUtc);
    var header = BuildHeader(currentInvoices, request, periodLabel);
    var kpis = BuildKpis(currentInvoices, previousInvoices);
    var riskRadar = BuildSupplierRiskRadar(currentInvoices);
    var profitVsAp = BuildProfitVsAp(currentInvoices);
    var monthlyTrend = BuildMonthlyPaymentTrend(currentInvoices);
    var cashFlow = BuildCashFlowForecast(currentInvoices, rangeEndUtc);
    var topSuppliers = BuildTopSuppliers(currentInvoices, outstandingOnly: false);
    var outstandingSuppliers = BuildTopSuppliers(currentInvoices, outstandingOnly: true);
    var departments = BuildDepartmentSpend(currentInvoices);
    var geography = BuildGeography(currentInvoices);
    var activeFilters = ApDashboardFilterSupport.BuildActiveFilters(request);

    return new ApDashboardResult(
      request.Period,
      periodLabel,
      rangeStartUtc,
      rangeEndUtc,
      header,
      kpis,
      riskRadar,
      profitVsAp,
      monthlyTrend,
      cashFlow,
      topSuppliers,
      outstandingSuppliers,
      departments,
      geography,
      filterOptions,
      activeFilters,
      request.IncludeInvoiceDetails ? currentInvoices : null);
  }

  private static ApDashboardHeaderDto BuildHeader(
    IReadOnlyList<ApDashboardInvoiceDto> invoices,
    ApDashboardRequest request,
    string periodLabel)
  {
    var outstanding = invoices
      .Where(i => !IsPaid(i.PaymentStatus))
      .Sum(i => i.Amount);

    var overdue = invoices
      .Where(i => string.Equals(i.PaymentStatus, "overdue", StringComparison.OrdinalIgnoreCase))
      .Sum(i => i.Amount);

    var open = invoices.Count(i => !IsPaid(i.PaymentStatus));

    var dpo = invoices
      .Where(i => i.InvoiceDate.HasValue && i.ProcessingDays.HasValue)
      .Select(i => (double)i.ProcessingDays!.Value)
      .DefaultIfEmpty(0)
      .Average();

    var dpoRounded = (decimal)Math.Round(dpo, 0);
    var context = BuildContextLabel(request, periodLabel);

    return new ApDashboardHeaderDto(
      outstanding,
      FormatMoney(outstanding),
      overdue,
      FormatMoney(overdue),
      open,
      dpoRounded,
      $"{dpoRounded:0}d",
      context);
  }

  private static string BuildContextLabel(ApDashboardRequest request, string periodLabel)
  {
    var periodPart = request.Period switch
    {
      ApDashboardPeriod.Today => "today",
      ApDashboardPeriod.Tomorrow => "tomorrow",
      ApDashboardPeriod.ThisWeek => "week",
      ApDashboardPeriod.ThisMonth => "month",
      ApDashboardPeriod.LastMonth => "last month",
      ApDashboardPeriod.ThisQuarter => "quarter",
      ApDashboardPeriod.ThisYear => "year",
      _ => periodLabel
    };

    var supplierPart = !string.IsNullOrWhiteSpace(request.Supplier)
        && !request.Supplier.Equals("all", StringComparison.OrdinalIgnoreCase)
      ? request.Supplier.Trim()
      : !string.IsNullOrWhiteSpace(request.Department)
        && !request.Department.Equals("all", StringComparison.OrdinalIgnoreCase)
        ? request.Department.Trim()
        : "all suppliers";

    return $"Real-time · {periodPart} · {supplierPart}";
  }

  private static ApDashboardSupplierRiskRadarDto BuildSupplierRiskRadar(
    IReadOnlyList<ApDashboardInvoiceDto> invoices)
  {
    var outstanding = invoices.Where(i => !IsPaid(i.PaymentStatus)).ToList();
    var bySupplier = outstanding
      .GroupBy(i => string.IsNullOrWhiteSpace(i.Supplier) ? "Unknown" : i.Supplier.Trim(), StringComparer.OrdinalIgnoreCase)
      .Select(g =>
      {
        var amount = g.Sum(x => x.Amount);
        var openCount = g.Count();
        var overdueCount = g.Count(x => string.Equals(x.PaymentStatus, "overdue", StringComparison.OrdinalIgnoreCase));
        var highCount = g.Count(x => string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase));
        var mediumCount = g.Count(x => string.Equals(x.RiskLevel, "medium", StringComparison.OrdinalIgnoreCase));
        var risk = ResolveSupplierRiskLevel(amount, overdueCount, openCount, highCount, mediumCount);
        return new ApDashboardSupplierRiskDto(
          g.Key,
          risk,
          amount,
          FormatMoney(amount),
          openCount,
          overdueCount,
          g.Select(x => x.CountryCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
          g.Select(x => x.Currency).FirstOrDefault() ?? "USD");
      })
      .OrderByDescending(x => RiskRank(x.RiskLevel))
      .ThenByDescending(x => x.OutstandingAmount)
      .ToList();

    var totalExposure = bySupplier.Sum(x => x.OutstandingAmount);
    var totalSuppliers = bySupplier.Count;

    var segments = new[] { "low", "medium", "high" }
      .Select(key =>
      {
        var bucket = bySupplier.Where(x => string.Equals(x.RiskLevel, key, StringComparison.OrdinalIgnoreCase)).ToList();
        var amount = bucket.Sum(x => x.OutstandingAmount);
        var percent = totalExposure <= 0 || totalSuppliers == 0
          ? 0m
          : Math.Round(bucket.Count * 100m / totalSuppliers, 0);
        return new ApDashboardRiskSegmentDto(
          key,
          key switch
          {
            "low" => "Low",
            "medium" => "Medium",
            _ => "High"
          },
          bucket.Count,
          amount,
          FormatMoney(amount),
          percent);
      })
      .ToList();

    return new ApDashboardSupplierRiskRadarDto(
      "Supplier Risk Radar",
      "Which vendors carry the most risk exposure?",
      totalSuppliers,
      totalExposure,
      FormatMoney(totalExposure),
      segments,
      bySupplier.Where(x => !string.Equals(x.RiskLevel, "low", StringComparison.OrdinalIgnoreCase)).Take(10).ToList());
  }

  private static string ResolveSupplierRiskLevel(
    decimal outstanding,
    int overdueCount,
    int openCount,
    int highInvoiceCount,
    int mediumInvoiceCount)
  {
    var score = 0;
    if (overdueCount > 0)
      score += Math.Min(3, overdueCount);
    if (highInvoiceCount > 0)
      score += 2;
    if (mediumInvoiceCount > 0)
      score += 1;
    if (outstanding >= 100_000m)
      score += 2;
    else if (outstanding >= 25_000m)
      score += 1;
    if (openCount >= 5)
      score += 1;

    if (score >= 4)
      return "high";
    if (score >= 2)
      return "medium";
    return "low";
  }

  private static int RiskRank(string risk) => risk.ToLowerInvariant() switch
  {
    "high" => 3,
    "medium" => 2,
    _ => 1
  };

  private static IReadOnlyList<ApDashboardKpiDto> BuildKpis(
    IReadOnlyList<ApDashboardInvoiceDto> current,
    IReadOnlyList<ApDashboardInvoiceDto> previous)
  {
    return
    [
      BuildKpi("total_outstanding", "Total Outstanding", current, previous,
        i => !IsPaid(i.PaymentStatus), i => i.Amount),
      BuildKpi("total_paid", "Total Paid", current, previous,
        i => IsPaid(i.PaymentStatus), i => i.Amount),
      BuildKpi("pending_payments", "Pending Payments", current, previous,
        i => string.Equals(i.PaymentStatus, "pending", StringComparison.OrdinalIgnoreCase), i => i.Amount),
      BuildKpi("due_today", "Due Today", current, previous,
        i => string.Equals(i.PaymentStatus, "due_today", StringComparison.OrdinalIgnoreCase), i => i.Amount),
      BuildKpi("overdue_amount", "Overdue", current, previous,
        i => string.Equals(i.PaymentStatus, "overdue", StringComparison.OrdinalIgnoreCase), i => i.Amount),
      BuildAvgProcessingKpi(current, previous)
    ];
  }

  private static ApDashboardKpiDto BuildKpi(
    string key,
    string label,
    IReadOnlyList<ApDashboardInvoiceDto> current,
    IReadOnlyList<ApDashboardInvoiceDto> previous,
    Func<ApDashboardInvoiceDto, bool> filter,
    Func<ApDashboardInvoiceDto, decimal> selector)
  {
    var value = current.Where(filter).Sum(selector);
    var prev = previous.Where(filter).Sum(selector);
    var change = ComputeChangePercent(value, prev);
    return new ApDashboardKpiDto(
      key,
      label,
      FormatMoney(value),
      value,
      change,
      TrendFromChange(change, invertGood: key is "overdue_amount" or "pending_payments" or "total_outstanding"));
  }

  private static ApDashboardKpiDto BuildAvgProcessingKpi(
    IReadOnlyList<ApDashboardInvoiceDto> current,
    IReadOnlyList<ApDashboardInvoiceDto> previous)
  {
    var value = current
      .Where(i => i.ProcessingDays.HasValue)
      .Select(i => i.ProcessingDays!.Value)
      .DefaultIfEmpty(0)
      .Average();

    var prev = previous
      .Where(i => i.ProcessingDays.HasValue)
      .Select(i => i.ProcessingDays!.Value)
      .DefaultIfEmpty(0)
      .Average();

    var change = ComputeChangePercent((decimal)value, (decimal)prev);
    return new ApDashboardKpiDto(
      "avg_processing_time",
      "Avg. Processing Time",
      $"{value:0.0} d",
      (decimal)value,
      change,
      TrendFromChange(change, invertGood: true));
  }

  private static ApDashboardSeriesDto BuildProfitVsAp(IReadOnlyList<ApDashboardInvoiceDto> invoices)
  {
    var points = invoices
      .Where(i => i.InvoiceDate.HasValue)
      .GroupBy(i => new { i.InvoiceDate!.Value.Year, i.InvoiceDate!.Value.Month })
      .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
      .Select(g =>
      {
        var ap = g.Sum(x => x.Amount);
        var matched = g.Count(x => IsMatched(x.MatchedStatus));
        var matchRate = g.Count() == 0 ? 0m : Math.Round(matched * 100m / g.Count(), 1);
        var label = $"{CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month)}";
        return new ApDashboardSeriesPointDto(label, ap, matchRate, "currency", "percent");
      })
      .ToList();

    return new ApDashboardSeriesDto(
      "Profit vs AP spending",
      "Dual axis: AP amount and match-rate % (proxy for profit efficiency)",
      points);
  }

  private static ApDashboardSeriesDto BuildMonthlyPaymentTrend(IReadOnlyList<ApDashboardInvoiceDto> invoices)
  {
    var points = invoices
      .Where(i => i.InvoiceDate.HasValue)
      .GroupBy(i => new { i.InvoiceDate!.Value.Year, i.InvoiceDate!.Value.Month })
      .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
      .Select(g => new ApDashboardSeriesPointDto(
        CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month),
        g.Where(x => IsPaid(x.PaymentStatus)).Sum(x => x.Amount),
        null,
        "currency",
        null))
      .ToList();

    return new ApDashboardSeriesDto(
      "Monthly payment trend",
      "Cash leaving the building, month by month",
      points);
  }

  private static ApDashboardSeriesDto BuildCashFlowForecast(
    IReadOnlyList<ApDashboardInvoiceDto> invoices,
    DateTime rangeEndUtc)
  {
    var pending = invoices
      .Where(i => !IsPaid(i.PaymentStatus))
      .ToList();

    var points = new List<ApDashboardSeriesPointDto>();
    var weekStart = rangeEndUtc.Date;
    for (var w = 1; w <= 10; w++)
    {
      var weekEnd = weekStart.AddDays(7);
      var dueInWeek = pending
        .Where(i => i.DueDate.HasValue && i.DueDate.Value.Date >= weekStart && i.DueDate.Value.Date < weekEnd)
        .Sum(i => i.Amount);
      points.Add(new ApDashboardSeriesPointDto($"W{w}", dueInWeek, null, "currency", null));
      weekStart = weekEnd;
    }

    return new ApDashboardSeriesDto(
      "Cash flow forecast",
      "Liquidity projection and cash needs over next 10 weeks",
      points);
  }

  private static IReadOnlyList<ApDashboardSupplierAmountDto> BuildTopSuppliers(
    IReadOnlyList<ApDashboardInvoiceDto> invoices,
    bool outstandingOnly)
  {
    var filtered = outstandingOnly
      ? invoices.Where(i => !IsPaid(i.PaymentStatus))
      : invoices.AsEnumerable();

    return filtered
      .GroupBy(i => string.IsNullOrWhiteSpace(i.Supplier) ? "Unknown" : i.Supplier.Trim(), StringComparer.OrdinalIgnoreCase)
      .Select(g => new ApDashboardSupplierAmountDto(
        g.Key,
        g.Sum(x => x.Amount),
        g.Select(x => x.Currency).FirstOrDefault() ?? "USD"))
      .OrderByDescending(x => x.Amount)
      .Take(10)
      .ToList();
  }

  private static IReadOnlyList<ApDashboardDepartmentSpendDto> BuildDepartmentSpend(
    IReadOnlyList<ApDashboardInvoiceDto> invoices)
  {
    var total = invoices.Sum(i => i.Amount);
    if (total <= 0)
      return [];

    return invoices
      .GroupBy(i => string.IsNullOrWhiteSpace(i.Department) ? "General" : i.Department.Trim(), StringComparer.OrdinalIgnoreCase)
      .Select(g =>
      {
        var amount = g.Sum(x => x.Amount);
        return new ApDashboardDepartmentSpendDto(
          g.Key,
          amount,
          Math.Round(amount * 100m / total, 0),
          g.Select(x => x.Currency).FirstOrDefault() ?? "USD");
      })
      .OrderByDescending(x => x.Amount)
      .ToList();
  }

  private static IReadOnlyList<ApDashboardGeographyDto> BuildGeography(
    IReadOnlyList<ApDashboardInvoiceDto> invoices)
  {
    var total = invoices.Sum(i => i.Amount);
    if (total <= 0)
      return [];

    return invoices
      .GroupBy(i => NormalizeCountry(i.CountryCode), StringComparer.OrdinalIgnoreCase)
      .Select(g =>
      {
        var amount = g.Sum(x => x.Amount);
        var suppliers = g.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return new ApDashboardGeographyDto(
          g.Key,
          CountryName(g.Key),
          amount,
          suppliers,
          Math.Round(amount * 100m / total, 0),
          g.Select(x => x.Currency).FirstOrDefault() ?? "USD");
      })
      .OrderByDescending(x => x.Amount)
      .ToList();
  }

  private static string BuildPeriodLabel(ApDashboardPeriod period, DateTime start, DateTime end) =>
    period switch
    {
      ApDashboardPeriod.Today => start.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
      ApDashboardPeriod.Tomorrow => start.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
      ApDashboardPeriod.ThisWeek => $"{start:dd MMM} – {end:dd MMM yyyy}",
      ApDashboardPeriod.ThisMonth => start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
      ApDashboardPeriod.LastMonth => start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
      ApDashboardPeriod.ThisQuarter => $"Q{((start.Month - 1) / 3) + 1} {start.Year}",
      ApDashboardPeriod.ThisYear => start.Year.ToString(CultureInfo.InvariantCulture),
      _ => $"{start:yyyy-MM-dd} – {end:yyyy-MM-dd}"
    };

  private static decimal? ComputeChangePercent(decimal current, decimal previous)
  {
    if (previous == 0)
      return current == 0 ? 0 : null;
    return Math.Round((current - previous) * 100m / previous, 1);
  }

  private static string? TrendFromChange(decimal? change, bool invertGood)
  {
    if (change is null or 0)
      return "flat";
    var up = change > 0;
    if (invertGood)
      up = !up;
    return up ? "up" : "down";
  }

  private static bool IsPaid(string status) =>
    string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
    || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

  private static bool IsMatched(string? status) =>
    !string.IsNullOrWhiteSpace(status)
    && (status.Contains("match", StringComparison.OrdinalIgnoreCase)
        || status.Contains("approv", StringComparison.OrdinalIgnoreCase));

  private static string FormatMoney(decimal value) =>
    value >= 1_000_000 ? $"${value / 1_000_000m:0.0}M"
    : value >= 1_000 ? $"${value / 1_000m:0.0}K"
    : $"${value:0}";

  private static string NormalizeCountry(string? code) =>
    string.IsNullOrWhiteSpace(code) ? "UN" : code.Trim().ToUpperInvariant();

  private static string CountryName(string code) => code switch
  {
    "US" => "United States",
    "DE" => "Germany",
    "IN" => "India",
    "GB" => "United Kingdom",
    "UN" => "Unknown",
    _ => code
  };
}
