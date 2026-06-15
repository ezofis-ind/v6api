using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, GetUserByIdQueryResult?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserByIdQueryResult?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return null;

        return new GetUserByIdQueryResult(user.Id, user.Email, user.DisplayName, user.Role, user.CreatedAtUtc,
            user.FirstName, user.LastName, user.PhoneNo, user.AuthStrategy, user.Department, user.JobTitle,
            user.Language, user.CountryCode, user.AvatarPath, user.UiPreference);
    }
}
