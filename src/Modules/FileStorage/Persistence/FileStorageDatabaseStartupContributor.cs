using FileStorage.Persistence;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Infrastructure.Persistence;
using SharedKernel.Infrastructure.Startup;

namespace FileStorage.Persistence;

internal sealed class FileStorageDatabaseStartupContributor : IDatabaseStartupContributor
{
    public int Order => 40;

    public async Task ApplyAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken
    )
    {
        FileStorageDbContext context = serviceProvider.GetRequiredService<FileStorageDbContext>();
        await context.EnsureCreatedAndTablesAsync(cancellationToken);
    }
}
