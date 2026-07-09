using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Users;

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

        var managerIds = users
            .Where(u => u.ManagerId != null)
            .Select(u => u.ManagerId!.Value)
            .Distinct()
            .ToList();

        var managers = await _userRepository.GetByIdsAsync(managerIds, cancellationToken);
        var managerEmails = managers.ToDictionary(m => m.Id, m => m.Email);

        var items = users
            .Select(u =>
            {
                string? managerEmail = null;
                if (u.ManagerId != null && managerEmails.TryGetValue(u.ManagerId.Value, out var email))
                    managerEmail = email;

                return UserExtendedResponseMapper.Map(u, managerEmail);
            })
            .ToList();

        return new ListUsersQueryResult(items);
    }
}
