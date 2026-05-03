using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Http;

// Binds to appsettings RateLimiting section. Three nested sub-sections keep global and
// named-policy budgets independent so they can be tuned separately per environment.
public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    // Baseline budget applied to every request via GlobalLimiter.
    [Required]
    public RateLimitPolicyOptions Global { get; init; } = new();

    // Fixed window budget for endpoints decorated with [EnableRateLimiting(RateLimitPolicies.Fixed)].
    [Required]
    public RateLimitPolicyOptions Fixed { get; init; } = new();

    // Sliding window budget for endpoints decorated with [EnableRateLimiting(RateLimitPolicies.Sliding)].
    [Required]
    public RateLimitPolicyOptions Sliding { get; init; } = new();
}

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

