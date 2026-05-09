using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using BuildingBlocks.Domain.Options;

namespace BuildingBlocks.Application.Options.Infrastructure;

/// <summary>
///     Application-level defaults for database transaction settings that can be overridden per call site.
///     Consumed by infrastructure components to build consistent <see cref="TransactionOptions" /> instances.
/// </summary>
public sealed class TransactionDefaultsOptions : IModuleOptions
{
    public static string SectionName => "TransactionDefaults";

    [Description("Default database isolation level applied when a transaction starts.")]
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    [Description("Default transaction timeout, in seconds.")]
    [Range(0, int.MaxValue)]
    public int TimeoutSeconds { get; set; } = 30;

    [Description("Enables automatic retry behavior for transient transaction failures.")]
    public bool RetryEnabled { get; set; } = true;

    [Description("Default number of retry attempts for transient transaction failures.")]
    [Range(0, int.MaxValue)]
    public int RetryCount { get; set; } = 3;

    [Description("Delay, in seconds, between transaction retry attempts.")]
    [Range(0, int.MaxValue)]
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    ///     Resolves the effective <see cref="TransactionOptions" /> by combining the configured defaults
    ///     in this instance with the specified <paramref name="overrides" />.
    /// </summary>
    /// <param name="overrides">
    ///     Optional per-call overrides. Any <c>null</c> or unset properties on <paramref name="overrides" />
    ///     will fall back to the corresponding default value defined on this <see cref="TransactionDefaultsOptions" />.
    /// </param>
    /// <returns>
    ///     A new <see cref="TransactionOptions" /> instance containing the resolved transaction settings.
    /// </returns>
    /// <remarks>
    ///     This method is intended to be used by infrastructure and other consumers that require
    ///     consistent transaction configuration based on application-level defaults plus optional,
    ///     context-specific overrides.
    /// </remarks>
    public TransactionOptions Resolve(TransactionOptions? overrides)
    {
        TransactionOptions resolved = new()
        {
            IsolationLevel = overrides?.IsolationLevel ?? IsolationLevel,
            TimeoutSeconds = overrides?.TimeoutSeconds ?? TimeoutSeconds,
            RetryEnabled = overrides?.RetryEnabled ?? RetryEnabled,
            RetryCount = overrides?.RetryCount ?? RetryCount,
            RetryDelaySeconds = overrides?.RetryDelaySeconds ?? RetryDelaySeconds,
        };

        ValidateNonNegative(resolved.TimeoutSeconds, nameof(TransactionOptions.TimeoutSeconds));
        ValidateNonNegative(resolved.RetryCount, nameof(TransactionOptions.RetryCount));
        ValidateNonNegative(
            resolved.RetryDelaySeconds,
            nameof(TransactionOptions.RetryDelaySeconds)
        );

        return resolved;
    }

    /// <summary>
    ///     Throws <see cref="ArgumentOutOfRangeException" /> when the given integer value is negative,
    ///     enforcing that transaction numeric settings are always non-negative.
    /// </summary>
    private static void ValidateNonNegative(int? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} cannot be negative."
            );
        }
    }
}
