using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.SoftDelete;
using SharedKernel.Infrastructure.StoredProcedures;
using SharedKernel.Infrastructure.UnitOfWork;
using Wolverine.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.Registration;

/// <summary>
/// Fluent registration surface for module infrastructure built on top of <see cref="ModuleDbContext"/>.
/// </summary>
public sealed class ModuleRegistrationBuilder<TContext>
    where TContext : DbContext
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;

    internal ModuleRegistrationBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public IServiceCollection Services => _services;

    public IConfiguration Configuration => _configuration;

    public ModuleRegistrationBuilder<TContext> ConfigureDbContext(
        Action<DbContextOptionsBuilder> configure
    )
    {
        _services.AddDbContextWithWolverineIntegration<TContext>(configure);
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddRepository<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.AddScoped<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.AddScoped<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddScoped<TService>()
        where TService : class
    {
        _services.AddScoped<TService>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.TryAddSingleton<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddSingleton<TService>()
        where TService : class
    {
        _services.TryAddSingleton<TService>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddCascadeRule<TRule>()
        where TRule : class, ISoftDeleteCascadeRule
    {
        _services.AddScoped<ISoftDeleteCascadeRule, TRule>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddStoredProcedureSupport()
    {
        _services.AddScoped<IStoredProcedureExecutor>(sp => new StoredProcedureExecutor(
            sp.GetRequiredService<TContext>()
        ));
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddDefaultInfrastructure()
    {
        _services.AddValidatedOptions<TransactionDefaultsOptions>(_configuration);

        _services.TryAddSingleton(TimeProvider.System);
        _services.TryAddSingleton<
            IEntityNormalizationService,
            PassthroughEntityNormalizationService
        >();
        _services.TryAddSingleton<IAuditableEntityStateManager, AuditableEntityStateManager>();
        _services.TryAddSingleton<ISoftDeleteProcessor, SoftDeleteProcessor>();

        _services.AddScoped<
            IDbTransactionProvider<TContext>,
            EfCoreTransactionProvider<TContext>
        >();
        _services.AddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();

        return this;
    }

    /// <summary>
    /// Registers a domain-layer marker type as an <see cref="IUnitOfWork{TMarker}"/> discriminator
    /// that forwards to the real <see cref="UnitOfWork{TContext}"/> for this module.
    /// Handlers use <c>IUnitOfWork&lt;TMarker&gt;</c> to resolve the correct unit of work
    /// without referencing the Infrastructure layer directly.
    /// </summary>
    public ModuleRegistrationBuilder<TContext> ForwardUnitOfWork<TMarker>()
    {
        _services.AddScoped<IUnitOfWork<TMarker>>(sp => new UnitOfWorkForwarder<TMarker>(
            sp.GetRequiredService<IUnitOfWork<TContext>>()
        ));
        return this;
    }

    private sealed class PassthroughEntityNormalizationService : IEntityNormalizationService
    {
        public void Normalize(Domain.Entities.Contracts.IAuditableTenantEntity entity) { }
    }
}
