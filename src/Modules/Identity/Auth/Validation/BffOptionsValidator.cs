using Identity.Auth.Options;
using Microsoft.Extensions.Options;

namespace Identity.Auth.Validation;

/// <summary>
///     Cross-field validation for <see cref="BffOptions" /> refresh coordination — data annotations on
///     individual properties cannot express these constraints.
/// </summary>
public sealed class BffOptionsValidator : IValidateOptions<BffOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BffOptions options)
    {
        List<string> failures = [];

        if (options.RefreshLockTimeoutMilliseconds >= options.RefreshWaitTimeoutMilliseconds)
        {
            failures.Add(
                "Bff:RefreshLockTimeoutMilliseconds must be less than Bff:RefreshWaitTimeoutMilliseconds."
            );
        }

        if (options.RefreshResultTtlMilliseconds < options.RefreshWaitTimeoutMilliseconds)
        {
            failures.Add(
                "Bff:RefreshResultTtlMilliseconds must be greater than or equal to Bff:RefreshWaitTimeoutMilliseconds."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
