namespace FileStorage.Contracts;

public sealed record FileDownloadResult(
    Stream FileStream,
    string ContentType,
    string FileName,
    string Sha256,
    DateTime CreatedAtUtc
);
