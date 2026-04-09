using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;

namespace FileStorage.Persistence;

public sealed class FileStorageDbContext : ModuleDbContext
{
    public FileStorageDbContext(
        DbContextOptions<FileStorageDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IEntityNormalizationService entityNormalizationService,
        IAuditableEntityStateManager entityStateManager
    )
        : base(
            options,
            tenantProvider,
            actorProvider,
            timeProvider,
            entityNormalizationService,
            entityStateManager
        ) { }

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("file_storage");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileStorageDbContext).Assembly);
    }
}
