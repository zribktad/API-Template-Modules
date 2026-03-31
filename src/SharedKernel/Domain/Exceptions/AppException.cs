namespace SharedKernel.Domain.Exceptions;

/// <summary>
/// Base class for all domain exceptions in this application.
/// Concrete subtypes map to specific HTTP status codes in the global exception handler.
/// </summary>
public abstract class AppException : Exception
{
    /// <summary>Optional machine-readable error code that callers can use for programmatic error handling.</summary>
    public string? ErrorCode { get; }

    /// <summary>Optional key-value bag of contextual data that can be included in the error response.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    protected AppException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
        : base(message)
    {
        ErrorCode = errorCode;
        Metadata = metadata;
    }
}
