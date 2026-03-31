using APITemplate.Application.Common.Errors;
using ErrorOr;
using FluentValidation;
using Wolverine;

namespace APITemplate.Application.Common.Middleware;

/// <summary>
/// Wolverine handler middleware that validates incoming messages using FluentValidation
/// and short-circuits with <see cref="ErrorOr{T}"/> errors instead of throwing exceptions.
/// Applied only to handlers whose return type is <c>ErrorOr&lt;T&gt;</c>.
/// </summary>
public static class ErrorOrValidationMiddleware
{
    /// <summary>
    /// Runs FluentValidation before the handler executes. If validation fails,
    /// returns <see cref="HandlerContinuation.Stop"/> with validation errors
    /// so the handler is never invoked.
    /// </summary>
    public static async Task<(HandlerContinuation, ErrorOr<TResponse>)> BeforeAsync<
        TMessage,
        TResponse
    >(TMessage message, IValidator<TMessage>? validator = null, CancellationToken ct = default)
    {
        if (validator is null)
            return (HandlerContinuation.Continue, default!);

        var validationResult = await validator.ValidateAsync(message, ct);

        if (validationResult.IsValid)
            return (HandlerContinuation.Continue, default!);

        var errors = validationResult
            .Errors.Select(e =>
            {
                var metadata = new Dictionary<string, object> { ["propertyName"] = e.PropertyName };
                if (e.AttemptedValue is not null)
                    metadata["attemptedValue"] = e.AttemptedValue;

                return Error.Validation(
                    code: ErrorCatalog.General.ValidationFailed,
                    description: e.ErrorMessage,
                    metadata: metadata
                );
            })
            .ToList();

        return (HandlerContinuation.Stop, errors);
    }
}
