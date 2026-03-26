using System.Text.RegularExpressions;
using APITemplate.Application.Common.BackgroundJobs;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APITemplate.Infrastructure.BackgroundJobs.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IReindexService"/> that rebuilds bloated
/// PostgreSQL full-text search indexes using <c>REINDEX INDEX CONCURRENTLY</c>.
/// Only indexes exceeding the configured bloat threshold are reindexed to minimise disruption.
/// </summary>
public sealed partial class ReindexService : IReindexService
{
    private const double BloatThresholdPercent = 30.0;

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReindexService> _logger;

    public ReindexService(AppDbContext dbContext, ILogger<ReindexService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Safety net for FTS index bloat after heavy write activity. PostgreSQL autovacuum
    /// handles routine maintenance, but cannot reclaim index bloat — only REINDEX can.
    /// This method checks actual bloat ratio before reindexing to avoid unnecessary work.
    /// Scoped to the current database's public schema to avoid touching other schemas.
    /// </summary>
    public async Task ReindexFullTextSearchAsync(CancellationToken ct = default)
    {
        var procedure = new GetFtsIndexNamesProcedure();
        var ftsIndexes = await _dbContext
            .Database.SqlQuery<string>(procedure.ToSql())
            .ToListAsync(ct);

        foreach (var index in ftsIndexes)
        {
            if (!ValidIndexNameRegex().IsMatch(index))
            {
                _logger.LogWarning("Skipping invalid FTS index name: {IndexName}.", index);
                continue;
            }

            var bloatPercent = await GetIndexBloatPercentAsync(index, ct);

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

            // REINDEX INDEX CONCURRENTLY is DDL — cannot be wrapped in a PostgreSQL function.
            // Identifier names cannot be parameterized here; regex validation above constrains
            // the value to PostgreSQL-safe identifier characters before interpolation.
#pragma warning disable EF1002
            await _dbContext.Database.ExecuteSqlRawAsync(
                $"REINDEX INDEX CONCURRENTLY \"{index}\"",
                ct
            );
#pragma warning restore EF1002

            _logger.LogInformation("Reindexed FTS index {IndexName}.", index);
        }
    }

    /// <summary>Queries the stored procedure for the bloat percentage of the named index.</summary>
    private async Task<double> GetIndexBloatPercentAsync(string indexName, CancellationToken ct)
    {
        var procedure = new GetIndexBloatPercentProcedure(indexName);
        return await _dbContext
            .Database.SqlQuery<double>(procedure.ToSql())
            .FirstOrDefaultAsync(ct);
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidIndexNameRegex();
}
