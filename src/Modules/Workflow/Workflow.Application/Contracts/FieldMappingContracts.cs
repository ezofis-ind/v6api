using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaaSApp.Workflow.Application.Contracts;

public sealed class FieldMappingRequest
{
    [JsonPropertyName("excelSheets")]
    public List<FieldMappingExcelSheet> ExcelSheets { get; set; } = [];

    [JsonPropertyName("headerFields")]
    public List<FieldMappingFormField> HeaderFields { get; set; } = [];

    [JsonPropertyName("lineItemFields")]
    public List<FieldMappingFormField> LineItemFields { get; set; } = [];
}

public sealed class FieldMappingExcelSheet
{
    [JsonPropertyName("sheetName")]
    public string SheetName { get; set; } = string.Empty;

    [JsonPropertyName("columns")]
    public List<string> Columns { get; set; } = [];
}

public sealed class FieldMappingFormField
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;
}

public interface IFieldMappingService
{
    /// <summary>POST excel/form field definitions to the Python field-mapping API and return its JSON response.</summary>
    Task<JsonElement> MapFieldsAsync(FieldMappingRequest request, CancellationToken cancellationToken = default);
}
