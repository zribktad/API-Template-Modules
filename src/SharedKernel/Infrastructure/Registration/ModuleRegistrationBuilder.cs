using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.Configuration;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.StoredProcedures;
using SharedKernel.Infrastructure.UnitOfWork;
using Wolverine.EntityFrameworkCore;

namespace SharedKernel.Infrastructure.Registration;

/// <summary>
///     Fluent registration surface for module infrastructure built on top of <see cref="ModuleDbContext" />.
/// </summary>
public sealed class ModuleRegistrationBuilder<TContext>
    where TContext : DbContext
{
    internal ModuleRegistrationBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }

    public IServiceCollection Services { get; }

    public IConfiguration Configuration { get; }

    public ModuleRegistrationBuilder<TContext> ConfigureDbContext(
        Action<DbContextOptionsBuilder> configure
    )
    {
        Services.AddDbContextWithWolverineIntegration<TContext>(configure);
        return this;
    }

    public ModuleRegistrationBuilder<TContext> ConfigureDbContext(
        Action<IServiceProvider, DbContextOptionsBuilder> configure
    )
    {
        Services.AddDbContextWithWolverineIntegration<TContext>(configure);
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddRepository<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.AddScoped<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.AddScoped<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddScoped<TService>()
        where TService : class
    {
        Services.AddScoped<TService>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.TryAddSingleton<TService, TImplementation>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddSingleton<TService>()
        where TService : class
    {
        Services.TryAddSingleton<TService>();
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddStoredProcedureSupport()
    {
        Services.AddScoped<IStoredProcedureExecutor>(sp => new StoredProcedureExecutor(
            sp.GetRequiredService<TContext>()
        ));
        return this;
    }

    public ModuleRegistrationBuilder<TContext> AddDefaultInfrastructure()
    {
        Services.AddValidatedOptions<TransactionDefaultsOptions>(Configuration);

        Services.TryAddSingleton(TimeProvider.System);
        Services.TryAddSingleton<IAuditableEntityStateManager, AuditableEntityStateManager>();

        Services.AddScoped<IDbTransactionProvider<TContext>, EfCoreTransactionProvider<TContext>>();
        Services.AddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();

        return this;
    }

    /// <summary>
    ///     Registers a domain-layer marker type as an <see cref="IUnitOfWork{TMarker}" /> discriminator
    ///     that forwards to the real <see cref="UnitOfWork{TContext}" /> for this module.
    ///     Handlers use <c>IUnitOfWork&lt;TMarker&gt;</c> to resolve the correct unit of work
    ///     without referencing the Infrastructure layer directly.
    /// </summary>
    public ModuleRegistrationBuilder<TContext> ForwardUnitOfWork<TMarker>()
    {
        Services.AddScoped<IUnitOfWork<TMarker>>(sp => new UnitOfWorkForwarder<TMarker>(
            sp.GetRequiredService<IUnitOfWork<TContext>>()
        ));
        return this;
    }
}
