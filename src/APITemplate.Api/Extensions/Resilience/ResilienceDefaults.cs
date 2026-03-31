namespace APITemplate.Api.Extensions.Resilience;

internal static class ResilienceDefaults
{
    public const int MaxRetryAttempts =
        SharedKernel.Infrastructure.Resilience.ResilienceDefaults.MaxRetryAttempts;
    public static readonly TimeSpan ShortDelay =
        SharedKernel.Infrastructure.Resilience.ResilienceDefaults.ShortDelay;
    public static readonly TimeSpan LongDelay =
        SharedKernel.Infrastructure.Resilience.ResilienceDefaults.LongDelay;
}
