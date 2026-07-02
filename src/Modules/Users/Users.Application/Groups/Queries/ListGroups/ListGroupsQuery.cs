using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Groups.Queries.ListGroups;

public record ListGroupsQuery : IRequest<ListGroupsQueryResult>;

public record ListGroupsQueryResult(IReadOnlyList<GroupListItem> Groups);

public sealed class ListGroupsQueryHandler : IRequestHandler<ListGroupsQuery, ListGroupsQueryResult>
{
    private readonly IGroupRepository _groupRepository;

    public ListGroupsQueryHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<ListGroupsQueryResult> Handle(ListGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _groupRepository.ListAsync(cancellationToken);
        return new ListGroupsQueryResult(groups);
    }
}
