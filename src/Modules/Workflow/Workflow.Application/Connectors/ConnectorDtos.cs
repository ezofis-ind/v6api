namespace SaaSApp.Workflow.Application.Connectors;

public sealed record ConnectorDto(
    Guid Id,
    Guid TenantId,
    string? Name,
    string? ConnectorType,
    string? CredentialJson,
    string? DynamicCredentialJson,
    string? ResponseStatus,
    string? ResponseStatusCode,
    string? ResponseBody,
    string? CreatedAt,
    string? ModifiedAt,
    string CreatedBy,
    string? ModifiedBy,
    bool IsDeleted,
    bool Preference = false,
    string? CreatedByEmail = null,
    string? ModifiedByEmail = null);

public sealed record ConnectorUpsertRequest(
    string? Name,
    string? ConnectorType,
    string? CredentialJson,
    string? DynamicCredentialJson,
    string? ResponseStatus,
    string? ResponseStatusCode,
    string? ResponseBody,
    bool? Preference = null);

/// <summary>v5 POST /api/connector/all body (insCriteriawithFilter).</summary>
public sealed record ConnectorListRequest(
    List<ConnectorFilterGroup>? FilterBy = null,
    string Mode = "browse");

public sealed record ConnectorFilterGroup(
    string GroupCondition = "AND",
    List<ConnectorFilter>? Filters = null)
{
    public List<ConnectorFilter> Filters { get; init; } = Filters ?? new();
}

public sealed record ConnectorFilter(string Criteria, string Condition, string Value);

public sealed record ConnectorListResponse(IReadOnlyList<ConnectorDto> Items);
