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
        var hasName = !string.IsNullOrWhiteSpace(request.Name);
        var hasDescription = request.Description != null;
        var hasUsers = request.UserIds != null;

        if (!hasName && !hasDescription && !hasUsers)
            return Fail("No fields to update.");

        var group = await _groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group == null)
            return Fail("Group not found.", 404);

        if (hasName)
        {
            var groupName = request.Name!.Trim();
            if (await _groupRepository.ExistsByNameAsync(groupName, request.GroupId, cancellationToken))
                return Fail($"A group named '{groupName}' already exists.");

            group.Update(name: groupName);
        }

        if (hasDescription)
            group.Update(description: request.Description);

        if (hasUsers)
        {
            var userIds = request.UserIds!.Distinct().ToList();
            if (userIds.Count > 0)
            {
                var existingUserCount = await _userRepository.CountExistingByIdsAsync(userIds, cancellationToken);
                if (existingUserCount != userIds.Count)
                    return Fail("One or more users were not found in this tenant.", 404);
            }

            group.ReplaceUsers(userIds);
        }

        return new UpdateGroupCommandResult(Success: true, StatusCode: 204);
    }

    private static UpdateGroupCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
