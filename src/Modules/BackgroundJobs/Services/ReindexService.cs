using System.Text.RegularExpressions;
using BackgroundJobs.Logging;
using BackgroundJobs.Persistence;
using BackgroundJobs.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackgroundJobs.Services;

/// <summary>
///     Infrastructure implementation of <see cref="IReindexService" /> that rebuilds bloated
///     PostgreSQL full-text search indexes using <c>REINDEX INDEX CONCURRENTLY</c>.
///     Only indexes exceeding the configured bloat threshold are reindexed to minimise disruption.
/// </summary>
public sealed partial class ReindexService : IReindexService
{
    private const double BloatThresholdPercent = 30.0;

    private readonly BackgroundJobsDbContext _dbContext;
    private readonly IStoredProcedureExecutor<BackgroundJobsDbMarker> _executor;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(
        BackgroundJobsDbContext dbContext,
        IStoredProcedureExecutor<BackgroundJobsDbMarker> executor,
        ILogger<ReindexService> logger
    )
    {
        _dbContext = dbContext;
        _executor = executor;
        _logger = logger;
    }

    public async Task ReindexFullTextSearchAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> ftsIndexes = await _executor.ScalarManyAsync(
            new GetFtsIndexNamesProcedure(),
            ct
        );

        List<string> validIndexes = ftsIndexes
            .Where(index =>
            {
                if (ValidIndexNameRegex().IsMatch(index))
                    return true;
                _logger.SkippingInvalidFtsIndexName(index);
                return false;
            })
            .ToList();

        foreach (string index in validIndexes)
        {
            double bloatPercent = await GetIndexBloatPercentAsync(index, ct);

            if (bloatPercent < BloatThresholdPercent)
            {
                _logger.FtsIndexBloatBelowThreshold(index, bloatPercent, BloatThresholdPercent);
                continue;
            }

            _logger.FtsIndexBloatExceedsThreshold(index, bloatPercent, BloatThresholdPercent);

#pragma warning disable EF1002
            await _dbContext.Database.ExecuteSqlRawAsync(
                $"REINDEX INDEX CONCURRENTLY \"{index}\"",
                ct
            );
#pragma warning restore EF1002

            _logger.FtsIndexReindexed(index);
        }
    }

    private async Task<double> GetIndexBloatPercentAsync(string indexName, CancellationToken ct)
    {
        double? result = await _executor.ScalarFirstAsync(
            new GetIndexBloatPercentProcedure(indexName),
            ct
        );
        return result ?? 0.0;
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidIndexNameRegex();
}
