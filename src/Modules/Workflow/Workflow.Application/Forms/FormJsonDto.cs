namespace SaaSApp.Workflow.Application.Forms;

/// <summary>Designer form JSON (v5 fformJson parity).</summary>
public sealed class FormJsonDto
{
    public string? Uid { get; set; }
    public List<FormPanelDto>? Panels { get; set; }
    public List<FormPanelDto>? SecondaryPanels { get; set; }
    public FormSettingsDto? Settings { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class FormSettingsDto
{
    public FormGeneralDto? General { get; set; }
    public FormPublishDto? Publish { get; set; }
}

public sealed class FormGeneralDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Layout { get; set; }
    public string? Type { get; set; }
    public string[]? QrFields { get; set; }
    public string[]? UniqueColumns { get; set; }
    public string[]? SuperUser { get; set; }
    public string[]? EntryUser { get; set; }
}

public sealed class FormPublishDto
{
    public string? PublishOption { get; set; }
    public string? PublishSchedule { get; set; }
}

public sealed class FormPanelDto
{
    public string? Id { get; set; }
    public List<FormFieldDto>? Fields { get; set; }
}

public sealed class FormFieldDto
{
    public string? Id { get; set; }
    public string? Label { get; set; }
    public string? Type { get; set; }
    public FormFieldSettingsDto? Settings { get; set; }
}

public sealed class FormFieldSettingsDto
{
    public FormFieldSpecificDto? Specific { get; set; }
    public FormFieldValidationDto? Validation { get; set; }
}

public sealed class FormFieldValidationDto
{
    public string? FieldRule { get; set; }
    public string? DocumentExpiryField { get; set; }
    public string? ContentRule { get; set; }
    public string? Maximum { get; set; }
    public string? Minimum { get; set; }
}

public sealed class FormFieldSpecificDto
{
    public List<FormFieldDto>? TableColumns { get; set; }
    public int MappedPopupPanel { get; set; }
    public string? CustomOptions { get; set; }
}
