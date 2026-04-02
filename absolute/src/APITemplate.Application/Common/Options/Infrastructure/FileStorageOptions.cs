namespace APITemplate.Application.Common.Options.Infrastructure;

/// <summary>
/// Configuration for the local file-storage provider, including the base directory, upload size limit,
/// and allowed file extensions.
/// </summary>
public sealed class FileStorageOptions
{
    public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "api-template-files");
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
    public string[] AllowedExtensions { get; set; } =
    [".jpg", ".png", ".gif", ".pdf", ".csv", ".txt"];
}
