using MediatR;

namespace SaaSApp.Api.TenantAuth.Commands.ValidateOtp;

public sealed record ValidateOtpCommand(string Email, string OTP) : IRequest<ValidateOtpResult>;

public sealed record ValidateOtpResult(int StatusCode, string Message);
