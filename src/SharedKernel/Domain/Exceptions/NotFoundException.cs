namespace SharedKernel.Domain.Exceptions;

/// <summary>
/// Thrown when a requested entity cannot be found by the given identifier (HTTP 404).
/// The message is automatically formatted as "<c>{entityName} with id '{id}' not found.</c>".
/// </summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(
        string entityName,
        object id,
        string? errorCode = null,
        IReadOnlyDictionary<string, object?>? metadata = null
    )
        : base($"{entityName} with id '{id}' not found.", errorCode, metadata) { }
}
