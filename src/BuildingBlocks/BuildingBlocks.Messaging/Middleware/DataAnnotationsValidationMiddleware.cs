using System.ComponentModel.DataAnnotations;
using ErrorOr;
using BuildingBlocks.Application.Errors;
using BuildingBlocks.Application.Validation;
using Wolverine;

namespace BuildingBlocks.Application.Middleware;

/// <summary>
///     Wolverine handler middleware that validates incoming messages using Data Annotations
///     and short-circuits with <see cref="ErrorOr{T}" /> errors instead of throwing exceptions.
///     Applied only to handlers whose return type is <c>ErrorOr&lt;T&gt;</c>.
/// </summary>
public static class DataAnnotationsValidationMiddleware
{
    // Static field: Wolverine code-gen cannot resolve DI services alongside open-generic TMessage parameters.
    private static readonly IValidator _validator = new DataAnnotationsValidator();

    public static Task<(HandlerContinuation, ErrorOr<TResponse>)> BeforeAsync<TMessage, TResponse>(
        TMessage message
    )
        where TMessage : notnull
    {
        IReadOnlyList<ValidationResult> failures = _validator.Validate(message);

        if (failures.Count == 0)
        {
            return Task.FromResult((HandlerContinuation.Continue, default(ErrorOr<TResponse>)!));
        }

        List<Error> errors = failures
            .Select(f =>
                Error.Validation(
                    ErrorCatalog.General.ValidationFailed,
                    f.ErrorMessage ?? ValidationConstants.ValidationFailedMessage,
                    new Dictionary<string, object>
                    {
                        [ValidationConstants.PropertyNameKey] = string.Join(", ", f.MemberNames),
                    }
                )
            )
            .ToList();

        return Task.FromResult((HandlerContinuation.Stop, (ErrorOr<TResponse>)errors));
    }
}

