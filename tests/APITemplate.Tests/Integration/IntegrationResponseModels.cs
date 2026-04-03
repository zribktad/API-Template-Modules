namespace APITemplate.Tests.Integration;

public sealed record ApiErrorResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string ErrorCode,
    string TraceId
);

public sealed record ProductDataContractResponse(
    Guid Id,
    string Type,
    string Title,
    string? Description,
    DateTime CreatedAt,
    string? Format,
    long? FileSizeBytes
);
