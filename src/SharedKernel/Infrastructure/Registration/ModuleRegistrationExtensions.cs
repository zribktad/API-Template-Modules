using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Infrastructure.Registration;

/// <summary>
/// Entry points for configuring module infrastructure.
/// </summary>
public static class ModuleRegistrationExtensions
{
    public static ModuleRegistrationBuilder<TContext> AddModule<TContext>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TContext : DbContext =>
        new(services, configuration);
}
