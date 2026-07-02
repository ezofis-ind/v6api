namespace SaaSApp.Repository.Infrastructure.Options;

public sealed class RepositoryOcrOptions
{
    public const string SectionName = "Repository:Ocr";

    /// <summary>Python OCR endpoint (JSON body with base64 <c>file</c>).</summary>
    public string UploadForOcrApiUrl { get; set; } = string.Empty;

    public string OcrType { get; set; } = "ADVANCED";

    public string PageNo { get; set; } = "1";

    public string ValidateType { get; set; } = "1";

    public int TimeoutMinutes { get; set; } = 5;
}
