using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.Workflow.Application.Forms;
using SaaSApp.Workflow.Application.Workflows;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed partial class FormService
{
    public async Task<FormUpdateResult> UpdateFormAsync(
        string formId,
        FormJsonDto formJson,
        string rawJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            return new FormUpdateResult(FormUpdateStatus.NotFound, null, "Form not found.");

        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var tenantGuid = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var general = formJson.Settings?.General
            ?? throw new InvalidOperationException("settings.general is required.");
        var name = general.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("settings.general.name is required.");

        var publish = formJson.Settings?.Publish;
        var publishOption = publish?.PublishOption?.Trim() ?? "DRAFT";
        var isPublished = publishOption.Equals("PUBLISHED", StringComparison.OrdinalIgnoreCase);

        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");
        var modifiedBy = userId.ToString("D");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureFormSchemaAsync(connection, cancellationToken);

        var storedId = await ResolveStoredFormIdAsync(connection, tenantGuid, formId.Trim(), cancellationToken);
        if (storedId == null)
            return new FormUpdateResult(FormUpdateStatus.NotFound, null, "Form not found.");

        if (await FormNameExistsForOtherFormAsync(connection, tenantGuid, name, storedId, cancellationToken))
            return new FormUpdateResult(FormUpdateStatus.NameConflict, storedId, "form already Exist");

        var qrFields = general.QrFields is { Length: > 0 } ? string.Join(",", general.QrFields) : "";
        var uniqueColumns = general.UniqueColumns is { Length: > 0 } ? string.Join(",", general.UniqueColumns) : "";
        var superUser = general.SuperUser is { Length: > 0 } ? string.Join(",", general.SuperUser) : "";
        var entryUser = general.EntryUser is { Length: > 0 } ? string.Join(",", general.EntryUser) : "";

        await UpdateWFormRowAsync(
            connection,
            tenantGuid,
            storedId,
            general,
            publishOption,
            qrFields,
            uniqueColumns,
            superUser,
            entryUser,
            modifiedBy,
            now,
            cancellationToken);

        var jsonToStore = !string.IsNullOrWhiteSpace(rawJson)
            ? rawJson
            : JsonSerializer.Serialize(formJson, WorkflowJsonSerializerOptions.Storage);
        await _formJsonStorage.SaveFormJsonAsync(storedId, jsonToStore, cancellationToken);

        var securityUserIds = CollectSecurityUserIds(modifiedBy, general.SuperUser, general.EntryUser);
        await RefreshFormSecurityAsync(connection, storedId, securityUserIds, modifiedBy, now, cancellationToken);

        if (isPublished)
        {
            var panels = formJson.Panels ?? new List<FormPanelDto>();
            var secondaryPanels = formJson.SecondaryPanels ?? new List<FormPanelDto>();
            var fields = CollectEntryFields(panels);

            if (fields.Count == 0)
                return new FormUpdateResult(FormUpdateStatus.NotFound, storedId, "Formfields not found");

            await SyncFormControlsAsync(connection, storedId, panels, secondaryPanels, fields, modifiedBy, now, cancellationToken);
            await EnsureFormEntryTableAsync(connection, storedId, fields, cancellationToken);
        }

        _logger.LogInformation("Updated form {FormId} ({Name}), published={Published}", storedId, name, isPublished);
        return new FormUpdateResult(FormUpdateStatus.Updated, storedId, storedId);
    }

    public async Task<FormDeleteResult> DeleteFormAsync(string formId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            return new FormDeleteResult(FormDeleteStatus.NotFound, "Form not found.");

        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");
        var tenantGuid = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var userId = _currentUserProvider.GetUserId() ?? throw new InvalidOperationException("User context is required.");
        var modifiedBy = userId.ToString("D");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "wForm", cancellationToken))
            return new FormDeleteResult(FormDeleteStatus.NotFound, "Form not found.");

        var storedId = await ResolveStoredFormIdAsync(connection, tenantGuid, formId.Trim(), cancellationToken);
        if (storedId == null)
            return new FormDeleteResult(FormDeleteStatus.NotFound, "Form not found.");

        var wFormIdValue = GetWFormReferenceIdValue(storedId);
        var rows = await SoftDeleteWFormAsync(connection, tenantGuid, storedId, modifiedBy, now, cancellationToken);
        if (rows == 0)
            return new FormDeleteResult(FormDeleteStatus.NotFound, "Form not found.");

        if (await HasTableAsync(connection, "wFormControl", cancellationToken))
        {
            await using var cmd = new SqlCommand(
                "UPDATE dbo.wFormControl SET isDeleted = 1, modifiedBy = @ModifiedBy, modifiedAt = @ModifiedAt WHERE wFormId = @FormId AND isDeleted = 0",
                connection);
            cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            cmd.Parameters.AddWithValue("@ModifiedAt", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (await HasTableAsync(connection, "wFormSecurity", cancellationToken))
        {
            await using var cmd = new SqlCommand(
                "UPDATE dbo.wFormSecurity SET isDeleted = 1, modifiedBy = @ModifiedBy, modifiedAt = @ModifiedAt WHERE wFormId = @FormId AND isDeleted = 0",
                connection);
            cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            cmd.Parameters.AddWithValue("@ModifiedAt", now);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Soft-deleted form {FormId}", storedId);
        return new FormDeleteResult(FormDeleteStatus.Deleted, storedId);
    }

    private static async Task<string?> ResolveStoredFormIdAsync(
        SqlConnection connection,
        Guid tenantGuid,
        string formId,
        CancellationToken cancellationToken)
    {
        var tenantKey = await ResolveTenantKeyAsync(connection, tenantGuid, cancellationToken);
        var idSqlType = await GetColumnTypeAsync(connection, "wForm", "id", cancellationToken);
        var idValue = CoerceIdValue(formId, idSqlType);

        const string sql = """
            SELECT CONVERT(NVARCHAR(64), id)
            FROM dbo.wForm
            WHERE id = @Id AND tenantId = @TenantId AND isDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", idValue);
        cmd.Parameters.AddWithValue("@TenantId", tenantKey);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result == null || result == DBNull.Value ? null : Convert.ToString(result)?.Trim();
    }

    private static async Task<bool> FormNameExistsForOtherFormAsync(
        SqlConnection connection,
        Guid tenantGuid,
        string name,
        string excludeFormId,
        CancellationToken cancellationToken)
    {
        var tenantKey = await ResolveTenantKeyAsync(connection, tenantGuid, cancellationToken);
        var idSqlType = await GetColumnTypeAsync(connection, "wForm", "id", cancellationToken);
        var excludeId = CoerceIdValue(excludeFormId, idSqlType);

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.wForm
            WHERE isDeleted = 0 AND tenantId = @TenantId AND name = @Name AND id <> @ExcludeId;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TenantId", tenantKey);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@ExcludeId", excludeId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task UpdateWFormRowAsync(
        SqlConnection connection,
        Guid tenantGuid,
        string formId,
        FormGeneralDto general,
        string publishOption,
        string qrFields,
        string uniqueColumns,
        string superUser,
        string entryUser,
        string modifiedBy,
        string now,
        CancellationToken cancellationToken)
    {
        _ = tenantGuid;
        var idSqlType = await GetColumnTypeAsync(connection, "wForm", "id", cancellationToken);
        var idValue = CoerceIdValue(formId, idSqlType);

        var sets = new List<string>
        {
            "name = @Name",
            "description = @Description",
            "type = @Type",
            "layout = @Layout",
            "publishOption = @PublishOption",
            "qrFields = @QrFields",
            "uniqueColumns = @UniqueColumns",
            "superUser = @SuperUser",
            "entryUser = @EntryUser",
            "modifiedAt = @ModifiedAt",
            "modifiedBy = @ModifiedBy"
        };

        if (await HasColumnAsync(connection, "wForm", "isEdit", cancellationToken))
            sets.Add("isEdit = 1");

        var sql = $"UPDATE dbo.wForm SET {string.Join(", ", sets)} WHERE id = @Id AND isDeleted = 0;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", idValue);
        cmd.Parameters.AddWithValue("@Name", general.Name!.Trim());
        cmd.Parameters.AddWithValue("@Description", (object?)general.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Type", (object?)general.Type ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Layout", (object?)general.Layout ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PublishOption", publishOption);
        cmd.Parameters.AddWithValue("@QrFields", qrFields);
        cmd.Parameters.AddWithValue("@UniqueColumns", uniqueColumns);
        cmd.Parameters.AddWithValue("@SuperUser", superUser);
        cmd.Parameters.AddWithValue("@EntryUser", entryUser);
        cmd.Parameters.AddWithValue("@ModifiedAt", now);
        cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> SoftDeleteWFormAsync(
        SqlConnection connection,
        Guid tenantGuid,
        string formId,
        string modifiedBy,
        string now,
        CancellationToken cancellationToken)
    {
        var tenantKey = await ResolveTenantKeyAsync(connection, tenantGuid, cancellationToken);
        var idSqlType = await GetColumnTypeAsync(connection, "wForm", "id", cancellationToken);
        var idValue = CoerceIdValue(formId, idSqlType);

        const string sql = """
            UPDATE dbo.wForm
            SET isDeleted = 1, modifiedAt = @ModifiedAt, modifiedBy = @ModifiedBy
            WHERE id = @Id AND tenantId = @TenantId AND isDeleted = 0;
            """;

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", idValue);
        cmd.Parameters.AddWithValue("@TenantId", tenantKey);
        cmd.Parameters.AddWithValue("@ModifiedAt", now);
        cmd.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RefreshFormSecurityAsync(
        SqlConnection connection,
        string formId,
        List<string> userIds,
        string modifiedBy,
        string now,
        CancellationToken cancellationToken)
    {
        const string securityTable = "wFormSecurity";
        if (!await HasTableAsync(connection, securityTable, cancellationToken))
            return;

        var wFormIdValue = GetWFormReferenceIdValue(formId);

        await using (var softDel = new SqlCommand(
            "UPDATE dbo.wFormSecurity SET isDeleted = 1, modifiedBy = @ModifiedBy, modifiedAt = @ModifiedAt WHERE wFormId = @FormId AND isDeleted = 0",
            connection))
        {
            softDel.Parameters.AddWithValue("@FormId", wFormIdValue);
            softDel.Parameters.AddWithValue("@ModifiedBy", modifiedBy);
            softDel.Parameters.AddWithValue("@ModifiedAt", now);
            await softDel.ExecuteNonQueryAsync(cancellationToken);
        }

        var securityIdIsIdentity = await IsIdentityColumnAsync(connection, securityTable, "id", cancellationToken);

        foreach (var userId in userIds)
        {
            var cols = "wFormId, userId, createdAt, createdBy, isDeleted";
            var vals = "@FormId, @UserId, @CreatedAt, @CreatedBy, 0";
            int? secId = null;
            if (!securityIdIsIdentity)
            {
                secId = await GetNextNumericIdAsync(connection, securityTable, cancellationToken);
                cols = "id, " + cols;
                vals = "@Id, " + vals;
            }

            var insertSql = $"INSERT INTO dbo.[{securityTable}]({cols}) VALUES({vals});";
            await using var cmd = new SqlCommand(insertSql, connection);
            if (secId.HasValue)
                cmd.Parameters.AddWithValue("@Id", secId.Value);
            cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@CreatedAt", now);
            cmd.Parameters.AddWithValue("@CreatedBy", modifiedBy);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
