using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Workflow.Application.Contracts;

public enum FormCreateStatus
{
    Created = 1,
    NotFound = 2,
    NameConflict = 3
}

public sealed record FormCreateResult(FormCreateStatus Status, string? FormId, string? Message);

/// <summary>v5 form designer API — create, list, and get with stored JSON.</summary>
public interface IFormService
{
    Task<FormCreateResult> CreateFormAsync(FormJsonDto formJson, string rawJson, CancellationToken cancellationToken = default);

    Task<FormListResponse> ListAsync(CancellationToken cancellationToken = default);

    Task<FormAllResponse> QueryAllAsync(
        FormAllRequest request,
        Guid? currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<FormByIdResult?> GetByIdAsync(string formId, CancellationToken cancellationToken = default);

    Task<FormUpdateResult> UpdateFormAsync(string formId, FormJsonDto formJson, string rawJson, CancellationToken cancellationToken = default);

    Task<FormDeleteResult> DeleteFormAsync(string formId, CancellationToken cancellationToken = default);
}
