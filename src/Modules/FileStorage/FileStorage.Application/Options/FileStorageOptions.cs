using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FileStorage.Application.Options;

/// <summary>
/// Configuration for the local file-storage provider, including the base directory, upload size limit,
/// and allowed file extensions.
/// </summary>
public sealed class FileStorageOptions
{
    [Description("Base directory where files are stored by the local file storage provider.")]
    [Required]
    [MinLength(1)]
    public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "api-template-files");

    [Description("Maximum allowed uploaded file size, in bytes.")]
    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    [Description("Allowed file extensions for uploaded files.")]
    [Required]
    [MinLength(1)]
    public string[] AllowedExtensions { get; set; } =
    [".jpg", ".png", ".gif", ".pdf", ".csv", ".txt"];
}
