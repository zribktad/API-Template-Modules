using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using BuildingBlocks.Domain.Options;

namespace BuildingBlocks.Infrastructure.EFCore.UnitOfWork;

/// <summary>
///     Selects the appropriate EF Core execution strategy based on provider type and transaction options.
/// </summary>
internal static class UnitOfWorkExecutionStrategyFactory
{
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
            null
        );
    }
}

