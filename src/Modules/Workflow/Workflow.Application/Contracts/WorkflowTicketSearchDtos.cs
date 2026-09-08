using System.Text.Json;

namespace SaaSApp.Workflow.Application.Contracts;

public sealed record WorkflowTicketFilterFieldDto(
    string Name,
    string SqlColumnName,
    string? DataType,
    IReadOnlyList<string> SupportedOperators);

public sealed record WorkflowTicketFilterSchemaDto(
    Guid WorkflowId,
    string? FormId,
    IReadOnlyList<WorkflowTicketFilterFieldDto> Fields);

/// <summary>
/// One filter clause for POST .../filter/search.
/// <list type="bullet">
/// <item><description><c>dataType</c> date: <c>value</c> may be a calendar date, or a preset such as <c>overdue</c>, <c>due today</c>, <c>next 7 days</c>.</description></item>
/// <item><description>Amount / high-value filters may send <c>0-8000</c>, <c>$1k-$5k</c>, <c>&lt; $1k</c> even when <c>dataType</c> is SHORT_TEXT.</description></item>
/// <item><description>Other dataTypes: <c>value</c> may be a string/number or a JSON array (e.g. for <c>in</c>).</description></item>
/// </list>
/// </summary>
public sealed record WorkflowTicketSearchFilter(
    string Criteria,
    string Condition,
    JsonElement Value = default,
    string? ValueTo = null,
    string? DataType = null);

public sealed record WorkflowTicketSearchSortBy(
    string Criteria = "raisedAt",
    string Order = "DESC");

public sealed record WorkflowTicketSearchRequest(
    IReadOnlyList<WorkflowTicketSearchFilter>? FilterBy = null,
    WorkflowTicketSearchSortBy? SortBy = null,
    string GroupBy = "",
    int CurrentPage = 1,
    int ItemsPerPage = 20);

public enum WorkflowTicketSearchStatus
{
    Found,
    WorkflowNotFound,
    FormNotConfigured,
    TablesMissing
}

public sealed record WorkflowTicketSearchOutcome(
    WorkflowTicketSearchStatus Status,
    WorkflowFilterSearchResult? Result);

public sealed record WorkflowFilterSearchGroup(
    string Key,
    IReadOnlyList<LegacyMailboxRowDto> Value);

public sealed record WorkflowFilterSearchMeta(
    int CurrentPage,
    int ItemsPerPage,
    int TotalItems);

public sealed record WorkflowFilterSearchResult(
    IReadOnlyList<WorkflowFilterSearchGroup> Data,
    WorkflowFilterSearchMeta Meta,
    bool TableExists);
