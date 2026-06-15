namespace SaaSApp.Workflow.Application.Forms;

/// <summary>GET /api/form/all — form summary per row.</summary>
public sealed record FormListItem(
    string FormId,
    string FormName,
    string CreatedBy,
    string? ModifiedBy = null,
    /// <summary>Creator email from users.Users.</summary>
    string? CreatedByName = null,
    /// <summary>Modifier email if modifiedBy is set; otherwise creator email.</summary>
    string? ModifiedByName = null);

public sealed record FormListResponse(IReadOnlyList<FormListItem> Items);

public sealed record FormAllRequest(
    FormAllSortBy? SortBy = null,
    string GroupBy = "",
    List<FormAllFilterGroup>? FilterBy = null,
    int CurrentPage = 1,
    int ItemsPerPage = 20,
    string Mode = "browse",
    bool HasSecurity = true,
    bool HasReport = false);

public sealed record FormAllSortBy(string Criteria = "modifiedAt", string Order = "DESC");

public sealed record FormAllFilterGroup(string GroupCondition = "AND", List<FormAllFilter>? Filters = null)
{
    public List<FormAllFilter> Filters { get; init; } = Filters ?? new();
}

public sealed record FormAllFilter(string Criteria, string Condition, string Value);

public sealed record FormAllMeta(int CurrentPage, int ItemsPerPage, int TotalItems);

public sealed record FormAllGroup(string Key, List<FormAllItem> Value);

public sealed record FormAllResponse(List<FormAllGroup> Data, FormAllMeta Meta);

public sealed record FormAllItem(
    string Id,
    string Name,
    string? Description,
    string PublishOption,
    string CreatedBy,
    string? ModifiedBy,
    DateTime? CreatedAt,
    DateTime? ModifiedAt,
    string? CreatedByName = null,
    string? ModifiedByName = null);
