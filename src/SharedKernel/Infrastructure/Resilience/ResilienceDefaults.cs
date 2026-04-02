namespace SharedKernel.Infrastructure.Resilience;

/// <summary>
/// Shared retry defaults used when registering resilience pipelines.
/// </summary>
public static class ResilienceDefaults
{
    public const int MaxRetryAttempts = 3;
    public static readonly TimeSpan ShortDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LongDelay = TimeSpan.FromSeconds(2);
}
