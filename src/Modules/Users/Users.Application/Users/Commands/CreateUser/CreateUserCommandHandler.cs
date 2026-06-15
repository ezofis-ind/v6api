using MediatR;
using SaaSApp.Users.Domain.Entities;
using SaaSApp.Users.Application.Contracts;

namespace SaaSApp.Users.Application.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserCommandResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public CreateUserCommandHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<CreateUserCommandResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("TenantId is required to create a user.");

        var user = User.Create(tenantId, request.Email, request.DisplayName, request.Role, request.FirstName, request.LastName, request.AuthStrategy ?? User.AuthStrategyEzofis);
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()));
        await _userRepository.AddAsync(user, cancellationToken);
        // SaveChanges + domain event dispatch is performed by TransactionBehavior
        return new CreateUserCommandResult(user.Id);
    }
}
