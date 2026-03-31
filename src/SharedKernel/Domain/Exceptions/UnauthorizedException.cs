namespace SharedKernel.Domain.Exceptions;

/// <summary>
/// Thrown when a request lacks valid authentication credentials required to access a resource (HTTP 401).
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string? errorCode = null)
        : base(message, errorCode) { }
}
