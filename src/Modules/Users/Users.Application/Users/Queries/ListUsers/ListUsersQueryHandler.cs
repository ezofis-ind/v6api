using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Queries.ListUsers;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, ListUsersQueryResult>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ListUsersQueryResult> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.ListAsync(cancellationToken);
        var items = users.Select(u => new ListUsersItem(u.Id, u.Email, u.DisplayName, u.Role, u.CreatedAtUtc, u.FirstName, u.LastName, u.AuthStrategy)).ToList();
        return new ListUsersQueryResult(items);
    }
}
