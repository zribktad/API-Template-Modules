namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ;

/// <summary>
/// String constants used as TickerQ function identifiers in <c>[TickerFunction]</c> attributes
/// and coordinator calls, ensuring consistent naming between registration and execution.
/// </summary>
internal static class TickerQFunctionNames
{
    public const string ExternalSync = "external-sync-recurring-job";
    public const string Cleanup = "cleanup-recurring-job";
    public const string Reindex = "reindex-recurring-job";
    public const string EmailRetry = "email-retry-recurring-job";
}
