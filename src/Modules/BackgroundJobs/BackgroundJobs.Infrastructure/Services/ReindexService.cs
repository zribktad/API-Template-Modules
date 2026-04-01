using System.Text.RegularExpressions;
using BackgroundJobs.Application.Services;
using BackgroundJobs.Infrastructure.Persistence;
using BackgroundJobs.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackgroundJobs.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IReindexService"/> that rebuilds bloated
/// PostgreSQL full-text search indexes using <c>REINDEX INDEX CONCURRENTLY</c>.
/// </summary>
public sealed partial class ReindexService : IReindexService
{
    private const double BloatThresholdPercent = 30.0;

    private readonly BackgroundJobsDbContext _dbContext;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(BackgroundJobsDbContext dbContext, ILogger<ReindexService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ReindexFullTextSearchAsync(CancellationToken ct = default)
    {
        GetFtsIndexNamesProcedure procedure = new();
        List<string> ftsIndexes = await _dbContext
            .Database.SqlQuery<string>(procedure.ToSql())
            .ToListAsync(ct);

        List<string> validIndexes = ftsIndexes
            .Where(index =>
            {
                if (ValidIndexNameRegex().IsMatch(index))
                    return true;
                _logger.LogWarning("Skipping invalid FTS index name: {IndexName}.", index);
                return false;
            })
            .ToList();

        foreach (string index in validIndexes)
        {
            double bloatPercent = await GetIndexBloatPercentAsync(index, ct);

            if (bloatPercent < BloatThresholdPercent)
            {
                _logger.LogDebug(
                    "FTS index {IndexName} bloat {BloatPercent:F1}% is below threshold {Threshold}%, skipping.",
                    index,
                    bloatPercent,
                    BloatThresholdPercent
                );
                continue;
            }

            _logger.LogInformation(
                "FTS index {IndexName} bloat {BloatPercent:F1}% exceeds threshold {Threshold}%, reindexing.",
                index,
                bloatPercent,
                BloatThresholdPercent
            );

#pragma warning disable EF1002
            await _dbContext.Database.ExecuteSqlRawAsync(
                $"REINDEX INDEX CONCURRENTLY \"{index}\"",
                ct
            );
#pragma warning restore EF1002

            _logger.LogInformation("Reindexed FTS index {IndexName}.", index);
        }
    }

    private async Task<double> GetIndexBloatPercentAsync(string indexName, CancellationToken ct)
    {
        GetIndexBloatPercentProcedure procedure = new(indexName);
        return await _dbContext
            .Database.SqlQuery<double>(procedure.ToSql())
            .FirstOrDefaultAsync(ct);
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidIndexNameRegex();
}
