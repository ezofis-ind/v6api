using System.Globalization;
using SaaSApp.Workflow.Application.Workflows.Commands.CreateWorkflow;

namespace SaaSApp.Workflow.Application.Workflows;

internal static class WorkflowInitiateLinksHelper
{
    public static (string? RepositoryId, string? FormId) FromInitiateUsing(WorkflowInitiateUsingDto? initiateUsing)
    {
        if (initiateUsing == null)
            return (null, null);

        string? repositoryId = null;
        if (initiateUsing.RepositoryId?.Guid is Guid repoGuid)
            repositoryId = repoGuid.ToString("D");
        else if (initiateUsing.RepositoryId?.LegacyInt is int legacyRepoId)
            repositoryId = legacyRepoId.ToString(CultureInfo.InvariantCulture);

        string? formId = null;
        if (initiateUsing.FormId?.Guid is Guid formGuid)
            formId = formGuid.ToString("D");
        else if (initiateUsing.FormId?.LegacyInt is int legacyFormId)
            formId = legacyFormId.ToString(CultureInfo.InvariantCulture);

        return (repositoryId, formId);
    }
}
