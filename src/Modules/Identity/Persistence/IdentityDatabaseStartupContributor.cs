using Microsoft.Extensions.DependencyInjection;
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
        IdentityDbContext context = serviceProvider.GetRequiredService<IdentityDbContext>();
        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
