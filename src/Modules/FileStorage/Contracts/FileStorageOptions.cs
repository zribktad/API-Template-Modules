using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace FileStorage.Contracts;

/// <summary>
///     Configuration for the content-addressed file-storage layer, including on-disk layout, upload size limit,
///     allowed file extensions, saga staging TTL, and orphan-blob reaper schedule.
/// </summary>
public sealed class FileStorageOptions
{
    [Description(
        "Base directory under which staging and blob subdirectories are created by default."
    )]
    [Required]
    [MinLength(1)]
    public string BasePath { get; set; } = Path.Combine(Path.GetTempPath(), "api-template-files");

    [Description(
        "Directory holding not-yet-committed staging writes. Defaults to {BasePath}/staging."
    )]
    public string? StagingPath { get; set; }

    [Description(
        "Directory holding content-addressed committed blobs. Defaults to {BasePath}/blobs."
    )]
    public string? BlobsPath { get; set; }

    [Description(
        "Blob-store backend key persisted on new StoredFile rows. Today only 'local' is supported."
    )]
    [Required]
    [MinLength(1)]
    [MaxLength(32)]
    public string BackendKey { get; set; } = "local";

    [Description("Maximum allowed uploaded file size, in bytes.")]
    [Range(1, long.MaxValue)]
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    [Description("Allowed file extensions for uploaded files.")]
    [Required]
    [MinLength(1)]
    public string[] AllowedExtensions { get; set; } =
    [".jpg", ".png", ".gif", ".pdf", ".csv", ".txt"];

    [Description(
        "Allow-list of MIME content types accepted at commit. Client-supplied ContentType is rejected "
            + "if not in this list — prevents stored-XSS via lying about content type."
    )]
    [Required]
    [MinLength(1)]
    public string[] AllowedContentTypes { get; set; } =
    ["image/jpeg", "image/png", "image/gif", "application/pdf", "text/csv", "text/plain"];

    [Description("Minutes a staged upload remains valid before TimeoutUploadCommand fires.")]
    [Range(1, 1440)]
    public int StagingTtlMinutes { get; set; } = 30;

    [Description("Hours a zero-refcount blob is retained before the orphan reaper deletes it.")]
    [Range(1, 720)]
    public int BlobRetentionHours { get; set; } = 24;

    public string ResolveStagingPath() =>
        string.IsNullOrWhiteSpace(StagingPath) ? Path.Combine(BasePath, "staging") : StagingPath;

    public string ResolveBlobsPath() =>
        string.IsNullOrWhiteSpace(BlobsPath) ? Path.Combine(BasePath, "blobs") : BlobsPath;
}
