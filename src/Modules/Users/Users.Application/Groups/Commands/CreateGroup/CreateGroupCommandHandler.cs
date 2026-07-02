using MediatR;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Domain.Entities;

namespace SaaSApp.Users.Application.Groups.Commands.CreateGroup;

public sealed class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, CreateGroupCommandResult>
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public CreateGroupCommandHandler(
        IGroupRepository groupRepository,
        IUserRepository userRepository,
        ITenantContext tenantContext)
    {
        _groupRepository = groupRepository;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<CreateGroupCommandResult> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("TenantId is required to create a group.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Fail("Group name is required.");

        var groupName = request.Name.Trim();
        if (request.UserIds == null || request.UserIds.Count == 0)
            return Fail("At least one user is required.");

        var userIds = request.UserIds.Distinct().ToList();
        var existingUserCount = await _userRepository.CountExistingByIdsAsync(userIds, cancellationToken);
        if (existingUserCount != userIds.Count)
            return Fail("One or more users were not found in this tenant.", 404);

        if (await _groupRepository.ExistsByNameAsync(groupName, cancellationToken: cancellationToken))
            return Fail($"A group named '{groupName}' already exists.");

        var group = Group.Create(tenantId, groupName, request.Description);
        group.AssignUsers(userIds);
        await _groupRepository.AddAsync(group, cancellationToken);

        return new CreateGroupCommandResult(
            Success: true,
            GroupId: group.Id,
            GroupName: group.Name,
            UserCount: userIds.Count,
            StatusCode: 201);
    }

    private static CreateGroupCommandResult Fail(string error, int statusCode = 400) =>
        new(Success: false, Error: error, StatusCode: statusCode);
}
