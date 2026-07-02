using MediatR;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Groups.Commands.UpdateGroup;

public sealed class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, UpdateGroupCommandResult>
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;

    public UpdateGroupCommandHandler(IGroupRepository groupRepository, IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
    }

    public async Task<UpdateGroupCommandResult> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Fail("Group name is required.");

        var groupName = request.Name.Trim();
        var userIds = (request.UserIds ?? []).Distinct().ToList();

        if (userIds.Count > 0)
        {
            var existingUserCount = await _userRepository.CountExistingByIdsAsync(userIds, cancellationToken);
            if (existingUserCount != userIds.Count)
                return Fail("One or more users were not found in this tenant.", 404);
        }

        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
            return Fail("Group not found.", 404);

        if (await _groupRepository.ExistsByNameAsync(groupName, request.GroupId, cancellationToken))
            return Fail($"A group named '{groupName}' already exists.");

        group.Update(groupName, request.Description);
        group.ReplaceUsers(userIds);

        return new UpdateGroupCommandResult(Success: true, StatusCode: 204);
    }

    private static UpdateGroupCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
