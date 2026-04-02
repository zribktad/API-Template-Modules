using ErrorOr;

namespace SharedKernel.Application.Errors;

/// <summary>
/// Factory methods producing <see cref="Error"/> instances for cross-cutting concerns.
/// Module-specific error factories live in each module's own Errors/DomainErrors.cs.
/// </summary>
public static class DomainErrors
{
    public static class General
    {
        public static Error NotFound(string entityName, Guid id) =>
            Error.NotFound(
                code: ErrorCatalog.General.NotFound,
                description: $"{entityName} with id '{id}' not found."
            );
    }
}
