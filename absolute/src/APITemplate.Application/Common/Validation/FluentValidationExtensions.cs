using APITemplate.Application.Common.Errors;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;

/// <summary>
/// Extension methods that integrate FluentValidation with the application's error-handling conventions.
/// </summary>
public static class FluentValidationExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> and throws a domain
    /// <see cref="Domain.Exceptions.ValidationException"/> when validation fails,
    /// aggregating all error messages into a single semicolon-delimited string.
    /// </summary>
    public static async Task ValidateAndThrowAppAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken ct = default,
        string? errorCode = null
    )
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
            throw new Domain.Exceptions.ValidationException(
                string.Join("; ", result.Errors.Select(e => e.ErrorMessage)),
                errorCode ?? ErrorCatalog.General.ValidationFailed
            );
    }
}
