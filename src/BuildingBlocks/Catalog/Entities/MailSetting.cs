namespace SaaSApp.Catalog.Entities;

public sealed class MailSetting
{
    public int SettingId { get; set; }
    public string EmailId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OutgoingServer { get; set; } = string.Empty;
    public int OutgoingPort { get; set; }
    public bool Isdeleted { get; set; }
    public int Preference { get; set; }
}
