namespace FileStorage.Contracts;

public sealed record FileDownloadResult(
    Stream FileStream,
    string ContentType,
    string FileName,
    string Sha256,
    DateTimeOffset CreatedAtUtc
);
