using Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Startup;

namespace APITemplate.Api.Extensions.Startup;

public static class DatabaseStartupExtensions
{
    public static async Task UseDatabaseAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default
    )
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;

        foreach (
            IDatabaseStartupContributor contributor in sp.GetServices<IDatabaseStartupContributor>()
                .OrderBy(c => c.Order)
        )
            await contributor.ApplyAsync(sp, cancellationToken);

        AuthBootstrapSeeder seeder = sp.GetRequiredService<AuthBootstrapSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
