using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSApp.Catalog.Persistence;

namespace SaaSApp.Api.TenantAuth.Commands.ValidateOtp;

public sealed class ValidateOtpCommandHandler : IRequestHandler<ValidateOtpCommand, ValidateOtpResult>
{
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;

    public ValidateOtpCommandHandler(IDbContextFactory<CatalogDbContext> catalogFactory)
    {
        _catalogFactory = catalogFactory;
    }

    public async Task<ValidateOtpResult> Handle(ValidateOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.OTP))
            throw new ArgumentException("Email and OTP are required.");

        var email = request.Email.Trim().ToLowerInvariant();
        var otp = request.OTP.Trim();

        await using var catalog = await _catalogFactory.CreateDbContextAsync(cancellationToken);
        var userOtp = await catalog.OtpVerifications
            .FirstOrDefaultAsync(s => s.Email == email && s.OTP == otp && !s.IsDeleted, cancellationToken);

        if (userOtp == null)
            return new ValidateOtpResult(404, "Email or OTP is not valid");

        if (DateTime.UtcNow > userOtp.ValidateAt)
        {
            userOtp.Status = "OTP expired";
            userOtp.ModifiedAt = DateTime.UtcNow;
            await catalog.SaveChangesAsync(cancellationToken);
            return new ValidateOtpResult(400, "OTP expired");
        }

        userOtp.Status = "verified";
        userOtp.ModifiedAt = DateTime.UtcNow;
        await catalog.SaveChangesAsync(cancellationToken);

        return new ValidateOtpResult(200, "success");
    }
}
