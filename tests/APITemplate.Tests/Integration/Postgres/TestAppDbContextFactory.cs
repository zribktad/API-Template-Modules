using APITemplate.Api.Extensions;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel.Application.Context;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Infrastructure.UnitOfWork;

namespace APITemplate.Tests.Integration.Postgres;

using AppDbUnitOfWork = SharedKernel.Infrastructure.UnitOfWork.UnitOfWork<APITemplate.Infrastructure.Persistence.AppDbContext>;

internal static class TestAppDbContextFactory
{
    internal static async Task<AppDbContext> CreateAsync(
        string connectionString,
        Guid tenantId,
        Guid actorId,
        bool hasTenant,
        CancellationToken ct
    )
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceServiceCollectionExtensions.ConfigurePostgresDbContext(
            optionsBuilder,
            connectionString
        );
        var context = new AppDbContext(
            optionsBuilder.Options,
            new TestTenantProvider(tenantId, hasTenant),
            new TestActorProvider(actorId),
            TimeProvider.System,
            new AuditableEntityStateManager()
        );
        await context.Database.OpenConnectionAsync(ct);
        return context;
    }

    internal static AppDbUnitOfWork CreateUnitOfWork(AppDbContext ctx) =>
        new(
            ctx,
            Options.Create(new TransactionDefaultsOptions()),
            NullLogger<AppDbUnitOfWork>.Instance,
            new EfCoreTransactionProvider(ctx)
        );
}

internal sealed class TestTenantProvider(Guid tenantId, bool hasTenant) : ITenantProvider
{
    public Guid TenantId => tenantId;
    public bool HasTenant => hasTenant;
}

internal sealed class TestActorProvider(Guid actorId) : IActorProvider
{
    public Guid ActorId => actorId;
}
