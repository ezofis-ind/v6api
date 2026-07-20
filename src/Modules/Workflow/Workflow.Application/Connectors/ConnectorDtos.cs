namespace SaaSApp.Workflow.Application.Connectors;

public sealed record ConnectorDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string ProviderCode,
    string? ConfigJson,
    string OAuthStatus,
    string? ExternalAccountEmail,
    DateTime? TokenExpiresAtUtc,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    Guid CreatedBy,
    Guid? ModifiedBy,
    bool IsDeleted,
    string? CreatedByEmail = null,
    string? ModifiedByEmail = null);

public sealed record ConnectorUpsertRequest(
    string? Name,
    string? ProviderCode,
    string? ConfigJson,
    bool? IsDefault = null);

/// <summary>Filter/list body for POST /api/connector/all.</summary>
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
