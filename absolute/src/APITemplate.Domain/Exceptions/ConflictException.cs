namespace APITemplate.Domain.Exceptions;

/// <summary>
/// Thrown when a requested operation cannot proceed because it conflicts with the current state of an existing resource (HTTP 409).
/// </summary>
public sealed class ConflictException : AppException
{
    public ConflictException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
        : base(message, errorCode, metadata) { }
}
