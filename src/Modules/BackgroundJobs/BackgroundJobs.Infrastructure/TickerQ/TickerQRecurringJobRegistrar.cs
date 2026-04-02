using BackgroundJobs.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel.Application.BackgroundJobs;
using TickerQ.Utilities.Entities;

namespace BackgroundJobs.Infrastructure.TickerQ;

public sealed class TickerQRecurringJobRegistrar
{
    private const string SeedIdentifier = "APITemplate:TickerQ:Recurring";
    private const string InitIdentifierProperty = "InitIdentifier";
    private const string CreatedAtProperty = "CreatedAt";
    private const string UpdatedAtProperty = "UpdatedAt";

    private readonly TickerQSchedulerDbContext _dbContext;
    private readonly IEnumerable<IRecurringBackgroundJobRegistration> _registrations;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TickerQRecurringJobRegistrar> _logger;

    public TickerQRecurringJobRegistrar(
        TickerQSchedulerDbContext dbContext,
        IEnumerable<IRecurringBackgroundJobRegistration> registrations,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<TickerQRecurringJobRegistrar> logger
    )
    {
        _dbContext = dbContext;
        _registrations = registrations;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        List<RecurringBackgroundJobDefinition> definitions = _registrations
            .Select(x => x.Build(_serviceProvider))
            .ToList();
        Dictionary<Guid, CronTickerEntity> tickersById = (
            await _dbContext.Set<CronTickerEntity>().ToListAsync(ct)
        ).ToDictionary(x => x.Id);

        foreach (RecurringBackgroundJobDefinition definition in definitions)
        {
            if (!tickersById.TryGetValue(definition.Id, out CronTickerEntity? existing))
            {
                CronTickerEntity entity = new()
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
                EntityEntry<CronTickerEntity> entry = _dbContext.Entry(entity);
                entry.Property(InitIdentifierProperty).CurrentValue = SeedIdentifier;
                entry.Property(CreatedAtProperty).CurrentValue = now;
                entry.Property(UpdatedAtProperty).CurrentValue = now;
                continue;
            }

            int[] retryIntervals = definition.RetryIntervals ?? [];
            bool changed =
                existing.Function != definition.FunctionName
                || existing.Description != definition.Description
                || existing.Expression != definition.CronExpression
                || existing.IsEnabled != definition.Enabled
                || existing.Retries != definition.Retries
                || !existing.RetryIntervals.SequenceEqual(retryIntervals);

            if (!changed)
                continue;

            existing.Function = definition.FunctionName;
            existing.Description = definition.Description;
            existing.Expression = definition.CronExpression;
            existing.IsEnabled = definition.Enabled;
            existing.Retries = definition.Retries;
            existing.RetryIntervals = retryIntervals;

            EntityEntry<CronTickerEntity> existingEntry = _dbContext.Entry(existing);
            existingEntry.Property(InitIdentifierProperty).CurrentValue ??= SeedIdentifier;
            existingEntry.Property(UpdatedAtProperty).CurrentValue = now;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.TickerQJobDefinitionsSynchronized(definitions.Count);
    }
}
