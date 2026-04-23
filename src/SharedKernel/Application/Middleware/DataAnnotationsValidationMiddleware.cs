using System.ComponentModel.DataAnnotations;
using ErrorOr;
using SharedKernel.Application.Errors;
using SharedKernel.Application.Validation;
using Wolverine;

namespace SharedKernel.Application.Middleware;

/// <summary>
///     Wolverine handler middleware that validates incoming messages using Data Annotations
///     and short-circuits with <see cref="ErrorOr{T}" /> errors instead of throwing exceptions.
///     Applied only to handlers whose return type is <c>ErrorOr&lt;T&gt;</c>.
/// </summary>
public static class DataAnnotationsValidationMiddleware
{
    public static Task<(HandlerContinuation, ErrorOr<TResponse>)> BeforeAsync<TResponse>(
        Envelope envelope,
        IValidator validator
    )
    {
        IReadOnlyList<ValidationResult> failures = validator.Validate(envelope.Message!);

        if (failures.Count == 0)
            return Task.FromResult((HandlerContinuation.Continue, default(ErrorOr<TResponse>)!));

        List<Error> errors = failures
            .Select(f =>
                Error.Validation(
                    ErrorCatalog.General.ValidationFailed,
                    f.ErrorMessage ?? "Validation failed.",
                    new Dictionary<string, object>
                    {
                        ["propertyName"] = string.Join(", ", f.MemberNames),
                    }
                )
            )
            .ToList();

        return Task.FromResult((HandlerContinuation.Stop, (ErrorOr<TResponse>)errors));
    }
}
