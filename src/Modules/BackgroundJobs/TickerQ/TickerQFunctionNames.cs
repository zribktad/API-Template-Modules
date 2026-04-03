namespace BackgroundJobs.TickerQ;

internal static class TickerQFunctionNames
{
    public const string ExternalSync = "external-sync-recurring-job";
    public const string Cleanup = "cleanup-recurring-job";
    public const string Reindex = "reindex-recurring-job";
    public const string EmailRetry = "email-retry-recurring-job";
}
