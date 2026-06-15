using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Catalog;
using SaaSApp.Security;
using SaaSApp.Users.Application.Contracts;
using SaaSApp.Users.Application.Users.Commands.CreateUser;
using SaaSApp.Users.Application.Users.Commands.DeleteUser;
using SaaSApp.Users.Application.Users.Commands.UpdateUser;
using SaaSApp.Users.Application.Users.Queries.GetCurrentUser;
using SaaSApp.Users.Application.Users.Queries.GetUserById;
using SaaSApp.Users.Application.Users.Queries.ListUsers;

namespace SaaSApp.Api.Controllers;

/// <summary>User CRUD for the current tenant. Requires JWT and X-Tenant-Id.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.TenantUser)]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly IUserTenantRegistry _userTenantRegistry;

    public UsersController(IMediator mediator, ITenantContext tenantContext, IUserTenantRegistry userTenantRegistry)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _userTenantRegistry = userTenantRegistry;
    }

    /// <summary>Create a new user in the current tenant. Admin only. Optionally set password for Ezofis login.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(CreateUserCommandResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
        var command = new CreateUserCommand(request.Email, request.DisplayName, request.Password, request.Role, request.FirstName, request.LastName, request.AuthStrategy);
        var result = await _mediator.Send(command, cancellationToken);
        await _userTenantRegistry.AddOrUpdateAsync(request.Email, tenantId, request.Role ?? "TenantUser", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.UserId }, result);
    }

    /// <summary>Current user profile from users.Users (path is /api/usersession, not under /api/users/...).</summary>
    [HttpGet("/api/usersession")]
    [ProducesResponseType(typeof(CurrentUserDetailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserSession(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        if (_tenantContext.TenantId == null)
            return BadRequest(new { error = "Tenant not resolved. Send X-Tenant-Id header or ensure JWT includes tid." });

        var result = await _mediator.Send(new GetCurrentUserQuery(userId.Value), cancellationToken);
        if (result == null)
            return NotFound(new { error = "User not found in this tenant." });

        return Ok(result);
    }

    /// <summary>List all users in the current tenant (excluding soft-deleted).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListUsersQueryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListUsersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get a user by ID in the current tenant.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetUserByIdQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    /// <summary>Update a user's profile. Admin only. Only provided fields are updated.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserCommand(id, request.DisplayName, request.Role, request.FirstName, request.LastName, request.PhoneNo, request.Department, request.JobTitle, request.Language, request.CountryCode, request.AvatarPath, request.UiPreference), cancellationToken);
        if (!result.Found)
            return NotFound();
        return NoContent();
    }

    /// <summary>Soft-delete a user. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id), cancellationToken);
        if (!result.Found)
            return NotFound();
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var user = HttpContext.User;
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userId");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

/// <summary>Request to create a user. Email and DisplayName required. Password enables Ezofis login.</summary>
public record CreateUserRequest(string Email, string DisplayName, string? Password = null, string? Role = null, string? FirstName = null, string? LastName = null, string? AuthStrategy = null);

/// <summary>Request to update a user. Only non-null fields are updated.</summary>
public record UpdateUserRequest(string? DisplayName, string? Role, string? FirstName = null, string? LastName = null, string? PhoneNo = null, string? Department = null, string? JobTitle = null, string? Language = null, string? CountryCode = null, string? AvatarPath = null, string? UiPreference = null);
