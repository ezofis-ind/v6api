using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Groups.Commands.DeleteGroup;

public sealed class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, DeleteGroupCommandResult>
{
    private readonly IGroupRepository _groupRepository;

    public DeleteGroupCommandHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<DeleteGroupCommandResult> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
            return new DeleteGroupCommandResult(Found: false);

        group.SoftDelete();
        return new DeleteGroupCommandResult(Found: true);
    }
}
