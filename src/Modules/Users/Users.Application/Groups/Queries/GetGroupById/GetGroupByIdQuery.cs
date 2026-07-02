using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Groups.Queries.GetGroupById;

public record GetGroupByIdQuery(Guid GroupId) : IRequest<GetGroupByIdQueryResult?>;

public record GetGroupByIdQueryResult(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    IReadOnlyList<GroupMemberItem> Users);

public sealed class GetGroupByIdQueryHandler : IRequestHandler<GetGroupByIdQuery, GetGroupByIdQueryResult?>
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupByIdQueryHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<GetGroupByIdQueryResult?> Handle(GetGroupByIdQuery request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetDetailByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
            return null;

        return new GetGroupByIdQueryResult(
            group.Id,
            group.Name,
            group.Description,
            group.CreatedAtUtc,
            group.Users);
    }
}
