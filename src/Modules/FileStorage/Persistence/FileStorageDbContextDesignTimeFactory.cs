using BuildingBlocks.Infrastructure.EFCore.Persistence.DesignTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileStorage.Persistence;

/// <summary>
///     Design-time factory for <c>dotnet ef migrations</c> against <see cref="FileStorageDbContext" />.
/// </summary>
public sealed class FileStorageDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<FileStorageDbContext>
{
    public FileStorageDbContext CreateDbContext(string[] args)
    {
        string connectionString = DesignTimeConnectionStringResolver.Resolve();

        DbContextOptions<FileStorageDbContext> options =
            new DbContextOptionsBuilder<FileStorageDbContext>().UseNpgsql(connectionString).Options;

        return new FileStorageDbContext(
            options,
            DesignTimeServices.TenantProvider,
            DesignTimeServices.ActorProvider,
            TimeProvider.System,
            DesignTimeServices.AuditableEntityStateManager
        );
    }
}
