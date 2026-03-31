using APITemplate.Domain.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Factory that selects the appropriate EF Core execution strategy based on the provider type
/// and the retry configuration specified in <see cref="TransactionOptions"/>.
/// </summary>
internal static class UnitOfWorkExecutionStrategyFactory
{
    /// <summary>
    /// Returns a <see cref="NonRetryingExecutionStrategy"/> when retries are disabled,
    /// a <see cref="NpgsqlRetryingExecutionStrategy"/> for Npgsql providers, or the
    /// provider's default strategy otherwise.
    /// </summary>
    public static IExecutionStrategy Create(
        DbContext dbContext,
        TransactionOptions effectiveOptions
    )
    {
        if (effectiveOptions.RetryEnabled == false)
            return new NonRetryingExecutionStrategy(dbContext);

        if (!dbContext.Database.IsNpgsql())
            return dbContext.Database.CreateExecutionStrategy();

        return new NpgsqlRetryingExecutionStrategy(
            dbContext,
            effectiveOptions.RetryCount ?? 3,
            TimeSpan.FromSeconds(effectiveOptions.RetryDelaySeconds ?? 5),
            errorCodesToAdd: null
        );
    }
}
