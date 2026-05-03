using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

namespace Identity.Persistence;

internal sealed class IdentityDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 10;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        IdentityDbContext context = serviceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
