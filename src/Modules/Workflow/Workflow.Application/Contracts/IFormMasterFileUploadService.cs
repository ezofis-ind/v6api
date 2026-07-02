using SaaSApp.Workflow.Application.Forms;

namespace SaaSApp.Workflow.Application.Contracts;

/// <summary>v5 POST /api/form/uploadMasterFile — stage master CSV/XLSX and queue data import.</summary>
public interface IFormMasterFileUploadService
{
    Task<FormMasterFileUploadResult> UploadMasterFileAsync(
        FormMasterFileUploadRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);
}
