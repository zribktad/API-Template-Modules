using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Infrastructure.EFCore.Configurations;

/// <summary>
///     Maps PostgreSQL system column <c>xmin</c> as an EF Core optimistic concurrency token (<c>xid</c>).
/// </summary>
public static class PostgresOptimisticConcurrencyExtensions
{
    public static void ConfigurePostgresXminConcurrency<TEntity>(
        this EntityTypeBuilder<TEntity> builder
    )
        where TEntity : class
    {
        builder
            .Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

