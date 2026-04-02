namespace APITemplate.Domain.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts to access a resource or perform an action they are not authorized for (HTTP 403).
/// </summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message, string? errorCode = null)
        : base(message, errorCode) { }
}
