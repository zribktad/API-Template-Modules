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
    // Static field: Wolverine code-gen cannot resolve DI services alongside open-generic TMessage parameters.
    private static readonly IValidator Validator = new DataAnnotationsValidator();

    public static Task<(HandlerContinuation, ErrorOr<TResponse>)> BeforeAsync<TMessage, TResponse>(
        TMessage message
    )
        where TMessage : notnull
    {
        IReadOnlyList<ValidationResult> failures = Validator.Validate(message);

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
