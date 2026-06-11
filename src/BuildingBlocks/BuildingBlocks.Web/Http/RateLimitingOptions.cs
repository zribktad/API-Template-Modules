using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Application.Http;

// Binds to appsettings RateLimiting section. Three nested sub-sections keep global and
// named-policy budgets independent so they can be tuned separately per environment.
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    /// <summary>
    ///     If true, the application will attempt to use Redis/Dragonfly as a shared backplane
    ///     for rate limiting across a cluster. If false, or if Redis is not configured,
    ///     it will fall back to local in-memory limiting.
    /// </summary>
    public bool AllowDistributedRateLimiting { get; init; } = true;

    // Baseline budget applied to every request via GlobalLimiter.
    [Required]
    [ValidateObjectMembers]
    public RateLimitPolicyOptions Global { get; init; } = new();

    // Fixed window budget for endpoints decorated with [EnableRateLimiting(RateLimitPolicies.Fixed)].
    [Required]
    [ValidateObjectMembers]
    public RateLimitPolicyOptions Fixed { get; init; } = new();

    // Sliding window budget for endpoints decorated with [EnableRateLimiting(RateLimitPolicies.Sliding)].
    [Required]
    [ValidateObjectMembers]
    public RateLimitPolicyOptions Sliding { get; init; } = new();
}

[OptionsValidator]
public partial class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>;

public sealed class RateLimitPolicyOptions
{
    // Maximum permits (tokens or requests) allowed.
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 1000;

    // Time window or replenishment period.
    [Range(1, int.MaxValue)]
    public int WindowMinutes { get; init; } = 1;

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; init; } = 0;

    // --- Specific to Sliding Window ---
    // Number of segments per window. Higher means more precision but more memory.
    [Range(1, int.MaxValue)]
    public int SegmentsPerWindow { get; init; } = 4;

    // --- Specific to Token Bucket ---
    // How many tokens are added every WindowMinutes. Defaults to 1000.
    [Range(1, int.MaxValue)]
    public int TokensPerPeriod { get; init; } = 1000;
}
