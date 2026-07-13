using MediatR;

namespace SaaSApp.Users.Application.Users.Commands.CompleteUserConfiguration;

public sealed record CompleteUserConfigurationCommand(Guid UserId, string Message)
    : IRequest<CompleteUserConfigurationCommandResult>;

public sealed record CompleteUserConfigurationCommandResult(
    bool Found,
    int Configuration,
    string? Error = null);
