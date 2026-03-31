using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Application.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TickerQ.Utilities.Entities;

namespace APITemplate.Infrastructure.BackgroundJobs.TickerQ;

/// <summary>
/// Upserts all registered recurring job definitions into the TickerQ scheduler database at
/// application startup, keeping cron expressions, enablement flags, and metadata in sync
/// with the current configuration without requiring manual database edits.
/// </summary>
public sealed class TickerQRecurringJobRegistrar
{
    private const string SeedIdentifier = "APITemplate:TickerQ:Recurring";
    private const string InitIdentifierProperty = "InitIdentifier";
    private const string CreatedAtProperty = "CreatedAt";
    private const string UpdatedAtProperty = "UpdatedAt";

    private readonly TickerQSchedulerDbContext _dbContext;
    private readonly IEnumerable<IRecurringBackgroundJobRegistration> _registrations;
    private readonly BackgroundJobsOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TickerQRecurringJobRegistrar> _logger;

    public TickerQRecurringJobRegistrar(
        TickerQSchedulerDbContext dbContext,
        IEnumerable<IRecurringBackgroundJobRegistration> registrations,
        IOptions<BackgroundJobsOptions> options,
        TimeProvider timeProvider,
        ILogger<TickerQRecurringJobRegistrar> logger
    )
    {
        _dbContext = dbContext;
        _registrations = registrations;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Loads all <see cref="CronTickerEntity"/> rows from the database, inserts new ones and updates
    /// existing ones to match the current <see cref="IRecurringBackgroundJobRegistration"/> definitions,
    /// then saves changes in a single call.
    /// </summary>
    public async Task SyncAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var definitions = _registrations.Select(x => x.Build(_options)).ToList();
        var tickersById = (await _dbContext.Set<CronTickerEntity>().ToListAsync(ct)).ToDictionary(
            x => x.Id
        );

        foreach (var definition in definitions)
        {
            if (!tickersById.TryGetValue(definition.Id, out var existing))
            {
                var entity = new CronTickerEntity
                {
                    Id = definition.Id,
                    Function = definition.FunctionName,
                    Description = definition.Description,
                    Expression = definition.CronExpression,
                    IsEnabled = definition.Enabled,
                    Retries = definition.Retries,
                    RetryIntervals = definition.RetryIntervals ?? [],
                };
                _dbContext.Set<CronTickerEntity>().Add(entity);
                var entry = _dbContext.Entry(entity);
                StampMetadata(entry, now);
                continue;
            }

            existing.Function = definition.FunctionName;
            existing.Description = definition.Description;
            existing.Expression = definition.CronExpression;
            existing.IsEnabled = definition.Enabled;
            existing.Retries = definition.Retries;
            existing.RetryIntervals = definition.RetryIntervals ?? [];
            StampUpdatedMetadata(_dbContext.Entry(existing), now);
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Synchronized {Count} recurring TickerQ job definitions.",
            definitions.Count
        );
    }

    /// <summary>Sets <c>InitIdentifier</c>, <c>CreatedAt</c>, and <c>UpdatedAt</c> shadow properties for a new entity.</summary>
    private static void StampMetadata(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CronTickerEntity> entry,
        DateTime now
    )
    {
        entry.Property(InitIdentifierProperty).CurrentValue = SeedIdentifier;
        entry.Property(CreatedAtProperty).CurrentValue = now;
        entry.Property(UpdatedAtProperty).CurrentValue = now;
    }

    /// <summary>Refreshes <c>UpdatedAt</c> and initialises <c>InitIdentifier</c> if not already set on an existing entity.</summary>
    private static void StampUpdatedMetadata(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CronTickerEntity> entry,
        DateTime now
    )
    {
        entry.Property(InitIdentifierProperty).CurrentValue ??= SeedIdentifier;
        entry.Property(UpdatedAtProperty).CurrentValue = now;
    }
}
