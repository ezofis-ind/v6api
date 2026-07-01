using System.Globalization;
using Microsoft.Data.SqlClient;
using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Workflow.Infrastructure.Services;

public sealed partial class FormService
{
    public async Task<FormControlsResult?> GetControlsAsync(string formId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(formId))
            return null;

        var normalizedFormId = FormIdNaming.NormalizeFormId(formId);
        var connectionString = _tenantContext.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, "wFormControl", cancellationToken))
            return null;

        if (!await FormExistsAsync(connection, normalizedFormId, cancellationToken))
            return null;

        var wFormIdValue = await ResolveWFormIdParameterAsync(connection, normalizedFormId, cancellationToken);
        var controls = await LoadFormControlsAsync(connection, wFormIdValue, cancellationToken);
        if (controls.Count == 0 && !Equals(wFormIdValue, normalizedFormId))
            controls = await LoadFormControlsAsync(connection, normalizedFormId, cancellationToken);

        return new FormControlsResult(normalizedFormId, controls.Count, controls);
    }

    private static async Task<bool> FormExistsAsync(
        SqlConnection connection,
        string formId,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "wForm", cancellationToken))
            return true;

        const string sql = """
            SELECT TOP 1 1
            FROM dbo.wForm
            WHERE id = @FormId AND isDeleted = 0
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", formId);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }

    private static async Task<List<FormControlItem>> LoadFormControlsAsync(
        SqlConnection connection,
        object wFormIdValue,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                wFormId,
                jsonId,
                name,
                type,
                isMandatory,
                parentId,
                createdAt,
                modifiedAt,
                createdBy,
                modifiedBy,
                isDeleted,
                activityBy,
                activityOn,
                activityId,
                validationJson
            FROM dbo.wFormControl
            WHERE wFormId = @FormId AND isDeleted = 0
            ORDER BY parentId, id
            """;

        var controls = new List<FormControlItem>();
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@FormId", wFormIdValue);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            controls.Add(new FormControlItem(
                Id: reader.GetInt32(0),
                WFormId: reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                JsonId: reader.IsDBNull(2) ? null : reader.GetString(2),
                Name: reader.IsDBNull(3) ? null : reader.GetString(3),
                Type: reader.IsDBNull(4) ? null : reader.GetString(4),
                IsMandatory: !reader.IsDBNull(5) && reader.GetBoolean(5),
                ParentId: reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                CreatedAt: reader.IsDBNull(7) ? null : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture),
                ModifiedAt: reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture),
                CreatedBy: reader.IsDBNull(9) ? null : Convert.ToString(reader.GetValue(9), CultureInfo.InvariantCulture),
                ModifiedBy: reader.IsDBNull(10) ? null : Convert.ToString(reader.GetValue(10), CultureInfo.InvariantCulture),
                IsDeleted: !reader.IsDBNull(11) && reader.GetBoolean(11),
                ActivityBy: reader.IsDBNull(12) ? null : Convert.ToString(reader.GetValue(12), CultureInfo.InvariantCulture),
                ActivityOn: reader.IsDBNull(13) ? null : Convert.ToString(reader.GetValue(13), CultureInfo.InvariantCulture),
                ActivityId: reader.IsDBNull(14) ? null : reader.GetInt32(14),
                ValidationJson: reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return controls;
    }

    private static async Task<object> ResolveWFormIdParameterAsync(
        SqlConnection connection,
        string formId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'wFormControl' AND COLUMN_NAME = N'wFormId'
            """;
        await using var cmd = new SqlCommand(sql, connection);
        var type = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString()?.ToLowerInvariant();
        if (type is "int" or "bigint" or "smallint" or "tinyint")
        {
            if (int.TryParse(formId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n;

            var hex = new string(formId.Where(Uri.IsHexDigit).ToArray());
            if (hex.Length > 8)
                hex = hex[..8];
            if (uint.TryParse(hex.PadLeft(8, '0'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                return unchecked((int)u);
        }

        return formId;
    }
}
