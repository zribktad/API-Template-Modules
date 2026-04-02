namespace APITemplate.Api.Extensions.Resilience;

internal static class ResilienceDefaults
{
    public const int MaxRetryAttempts = 3;
    public static readonly TimeSpan ShortDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LongDelay = TimeSpan.FromSeconds(2);
}
