namespace SaaSApp.Catalog.Entities;

public sealed class OtpVerification
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string OTP { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ValidateAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
}
