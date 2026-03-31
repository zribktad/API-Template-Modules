namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ;

/// <summary>
/// Stable GUIDs that uniquely identify each recurring TickerQ job in the scheduler database.
/// These values must never change once the jobs have been seeded.
/// </summary>
internal static class TickerQJobIds
{
    public static readonly Guid ExternalSync = new("d3870105-2cdb-4d6c-a2a6-3843bd459018");
    public static readonly Guid Cleanup = new("4bc6790c-c877-43ed-8a32-85d5fa2dad95");
    public static readonly Guid Reindex = new("9cf4e6ef-a2dd-4ff7-8968-174a6236a59f");
    public static readonly Guid EmailRetry = new("31261201-e220-45d0-bd7e-6d662ca1acaf");
}
