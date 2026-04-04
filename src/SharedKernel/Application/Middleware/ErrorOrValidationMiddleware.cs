using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using SharedKernel.Application.Errors;
using Wolverine;

namespace SharedKernel.Application.Middleware;

/// <summary>
///     Wolverine handler middleware that validates incoming messages using FluentValidation
///     and short-circuits with <see cref="ErrorOr{T}" /> errors instead of throwing exceptions.
///     Applied only to handlers whose return type is <c>ErrorOr&lt;T&gt;</c>.
/// </summary>
public static class ErrorOrValidationMiddleware
{
    /// <summary>
    ///     Runs FluentValidation before the handler executes. If validation fails,
    ///     returns <see cref="HandlerContinuation.Stop" /> with validation errors
    ///     so the handler is never invoked.
    /// </summary>
    public static async Task<(HandlerContinuation, ErrorOr<TResponse>)> BeforeAsync<
        TMessage,
        TResponse
    >(TMessage message, IValidator<TMessage>? validator = null, CancellationToken ct = default)
    {
        if (validator is null)
            return (HandlerContinuation.Continue, default!);

        ValidationResult validationResult = await validator.ValidateAsync(message, ct);

        if (validationResult.IsValid)
            return (HandlerContinuation.Continue, default!);

        List<Error> errors = validationResult
            .Errors.Select(e =>
            {
                Dictionary<string, object> metadata = new() { ["propertyName"] = e.PropertyName };
                if (e.AttemptedValue is not null)
                    metadata["attemptedValue"] = e.AttemptedValue;

                return Error.Validation(
                    ErrorCatalog.General.ValidationFailed,
                    e.ErrorMessage,
                    metadata
                );
            })
            .ToList();

        return (HandlerContinuation.Stop, errors);
    }
}
