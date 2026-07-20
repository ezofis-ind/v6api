namespace SaaSApp.Workflow.Application.Connectors;

public sealed record MasterResolveItemDto(
    string Id,
    string Type,
    string? DisplayName,
    string? Email,
    string Source,
    string? ExternalId,
    object? Raw);

public sealed record MasterResolveResponse(
    string Type,
    string Source,
    IReadOnlyList<MasterResolveItemDto> Items);
