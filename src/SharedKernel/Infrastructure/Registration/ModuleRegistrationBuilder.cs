using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.SoftDelete;
using SharedKernel.Infrastructure.StoredProcedures;
using SharedKernel.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        Action<IServiceProvider, DbContextOptionsBuilder> configure
    )
    {
        _services.AddDbContext<TContext>(configure);
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
        _services.AddScoped<IStoredProcedureExecutor>(sp => 
            new StoredProcedureExecutor(sp.GetRequiredService<TContext>())
        );
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddDefaultInfrastructure()
    {
        _services.Configure<TransactionDefaultsOptions>(
            _configuration.SectionFor<TransactionDefaultsOptions>()
        );

        _services.TryAddSingleton(TimeProvider.System);
        _services.TryAddSingleton<IEntityNormalizationService, PassthroughEntityNormalizationService>();
        _services.TryAddSingleton<IAuditableEntityStateManager, AuditableEntityStateManager>();
        _services.TryAddSingleton<ISoftDeleteProcessor, SoftDeleteProcessor>();
        
        _services.AddScoped<IDbTransactionProvider<TContext>, EfCoreTransactionProvider<TContext>>();
        _services.AddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();

        return this;
    }

    private sealed class PassthroughEntityNormalizationService : IEntityNormalizationService
    {
        public void Normalize(Domain.Entities.Contracts.IAuditableTenantEntity entity) { }
    }
}
