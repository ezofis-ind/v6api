using MediatR;

namespace SaaSApp.Api.TenantAuth.Commands.CheckAuthenticate;

public sealed record CheckAuthenticateCommand(string Email, bool RequiredOTP = true) : IRequest<CheckAuthenticateResult>;

public sealed record CheckAuthenticateResult(int StatusCode, string Message);
