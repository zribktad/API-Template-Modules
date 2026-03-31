namespace SharedKernel.Domain.Exceptions;

/// <summary>
/// Thrown when input data fails domain or application validation rules (HTTP 422).
/// </summary>
public sealed class ValidationException : AppException
{
    public ValidationException(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
        : base(message, errorCode, metadata) { }
}
