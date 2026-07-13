using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Commands.CompleteUserConfiguration;

public sealed class CompleteUserConfigurationCommandHandler
    : IRequestHandler<CompleteUserConfigurationCommand, CompleteUserConfigurationCommandResult>
{
    public const string CompletedMessage = "configuration:completed";

    private readonly IUserRepository _userRepository;

    public CompleteUserConfigurationCommandHandler(IUserRepository userRepository) =>
        _userRepository = userRepository;

    public async Task<CompleteUserConfigurationCommandResult> Handle(
        CompleteUserConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        if (!IsCompletedMessage(request.Message))
        {
            return new CompleteUserConfigurationCommandResult(
                Found: false,
                Configuration: 0,
                Error: $"Message must be '{CompletedMessage}'.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return new CompleteUserConfigurationCommandResult(Found: false, Configuration: 0);

        user.MarkConfigurationCompleted();
        _userRepository.Update(user);

        return new CompleteUserConfigurationCommandResult(Found: true, Configuration: user.Configuration);
    }

    internal static bool IsCompletedMessage(string? message) =>
        string.Equals(message?.Trim(), CompletedMessage, StringComparison.OrdinalIgnoreCase);
}
