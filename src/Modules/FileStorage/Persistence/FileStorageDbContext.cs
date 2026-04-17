using FileStorage.Domain.Sagas;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Context;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.Persistence;

namespace FileStorage.Persistence;

public sealed class FileStorageDbContext : ModuleDbContext
{
    public FileStorageDbContext(
        DbContextOptions<FileStorageDbContext> options,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        IAuditableEntityStateManager entityStateManager
    )
        : base(options, tenantProvider, actorProvider, timeProvider, entityStateManager) { }

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<FileUploadSaga> FileUploadSagas => Set<FileUploadSaga>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("file_storage");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FileStorageDbContext).Assembly);
    }
}
