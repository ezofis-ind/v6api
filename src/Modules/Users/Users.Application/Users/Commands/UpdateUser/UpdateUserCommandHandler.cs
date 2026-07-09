using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserCommandResult>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UpdateUserCommandResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return new UpdateUserCommandResult(Found: false);

        user.Update(request.DisplayName, request.Role, request.FirstName, request.LastName, request.PhoneNo,
            request.Department, request.JobTitle, request.Language, request.CountryCode, request.AvatarPath, request.UiPreference);
        _userRepository.Update(user);
        return new UpdateUserCommandResult(Found: true);
    }
}
