using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSApp.Api.TenantAuth.Commands.CheckAuthenticate;
using SaaSApp.Api.TenantAuth.Commands.ValidateOtp;

namespace SaaSApp.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class TenantController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Route("api/tenant/checkAuthenticate")]
    public async Task<IActionResult> CheckAuthenticate([FromBody] CheckAuthenticateRequest inputValue, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new CheckAuthenticateCommand(inputValue.Email, inputValue.RequiredOTP), cancellationToken);
            return StatusCode(result.StatusCode, result.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost]
    [Route("api/tenant/validateOTP")]
    public async Task<IActionResult> ValidateOtp([FromBody] ValidateOtpRequest inputValue, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new ValidateOtpCommand(inputValue.Email, inputValue.OTP), cancellationToken);
            return StatusCode(result.StatusCode, result.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public sealed record CheckAuthenticateRequest(string Email, bool RequiredOTP = true);
public sealed record ValidateOtpRequest(string Email, string OTP);
