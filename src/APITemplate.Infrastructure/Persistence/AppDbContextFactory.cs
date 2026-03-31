using SharedKernel.Application.Context;
using APITemplate.Domain.Entities;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used exclusively by EF Core tooling (dotnet ef migrations add/remove).
/// Not used at runtime.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates an <see cref="AppDbContext"/> with null-object collaborators so EF Core tooling
    /// can scaffold and apply migrations without a running application host.
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfigurationHelper.BuildConfiguration();
        var connectionString = DesignTimeConfigurationHelper.GetConnectionString(configuration);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(
            options,
            new NullTenantProvider(),
            new NullActorProvider(),
            TimeProvider.System,
            [],
            new NullEntityNormalizationService(),
            new NullAuditableEntityStateManager(),
            new NullSoftDeleteProcessor()
        );
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class NullActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }

    private sealed class NullEntityNormalizationService : IEntityNormalizationService
    {
        public void Normalize(IAuditableTenantEntity entity) { }
    }

    private sealed class NullAuditableEntityStateManager : IAuditableEntityStateManager
    {
        public void StampAdded(
            EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor,
            bool hasTenant,
            Guid currentTenantId
        ) { }

        public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor) { }

        public void MarkSoftDeleted(
            EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor
        ) { }
    }

    private sealed class NullSoftDeleteProcessor : ISoftDeleteProcessor
    {
        public Task ProcessAsync(
            DbContext dbContext,
            EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor,
            IReadOnlyCollection<SharedKernel.Infrastructure.SoftDelete.ISoftDeleteCascadeRule> softDeleteCascadeRules,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }
}
