using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SharedKernel.Application.Context;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Infrastructure.Auditing;
using SharedKernel.Infrastructure.EntityNormalization;
using SharedKernel.Infrastructure.SoftDelete;

namespace Notifications.Persistence;

/// <summary>
///     Design-time factory for <c>dotnet ef migrations</c> against <see cref="NotificationsDbContext" />.
/// </summary>
public sealed class NotificationsDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        string configurationBasePath = ResolveApiConfigurationDirectory();

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(configurationBasePath)
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=notifications_ef_design;Username=postgres;Password=postgres";

        DbContextOptions<NotificationsDbContext> options =
            new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new NotificationsDbContext(
            options,
            new DesignTimeTenantProvider(),
            new DesignTimeActorProvider(),
            TimeProvider.System,
            [],
            new DesignTimeEntityNormalizationService(),
            new DesignTimeAuditableEntityStateManager(),
            new DesignTimeSoftDeleteProcessor()
        );
    }

    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class DesignTimeActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }

    private sealed class DesignTimeEntityNormalizationService : IEntityNormalizationService
    {
        public void Normalize(IAuditableTenantEntity entity) { }
    }

    private sealed class DesignTimeAuditableEntityStateManager : IAuditableEntityStateManager
    {
        public void StampAdded(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor,
            bool hasTenant,
            Guid currentTenantId
        ) { }

        public void StampModified(IAuditableTenantEntity entity, DateTime now, Guid actor) { }

        public void MarkSoftDeleted(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor
        ) { }
    }

    private sealed class DesignTimeSoftDeleteProcessor : ISoftDeleteProcessor
    {
        public Task ProcessAsync(
            DbContext dbContext,
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
            IAuditableTenantEntity entity,
            DateTime now,
            Guid actor,
            IReadOnlyCollection<ISoftDeleteCascadeRule> softDeleteCascadeRules,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;
    }

    /// <summary>
    ///     Locates <c>APITemplate/Api/appsettings.json</c> by walking up from the process cwd and from this
    ///     assembly directory so <c>dotnet ef</c> works from the repo root, module folder, or <c>bin/</c> output.
    /// </summary>
    private static string ResolveApiConfigurationDirectory()
    {
        foreach (string root in GetDesignTimeSearchRoots())
        {
            DirectoryInfo? dir = new(root);
            while (dir is not null)
            {
                foreach (
                    string candidate in new[]
                    {
                        Path.Combine(dir.FullName, "src", "APITemplate", "Api"),
                        Path.Combine(dir.FullName, "APITemplate", "Api"),
                    }
                )
                {
                    if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not find APITemplate/Api/appsettings.json. "
                + "Run `dotnet ef` from the repository tree, or set ConnectionStrings__DefaultConnection."
        );
    }

    private static IEnumerable<string> GetDesignTimeSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();

        string? assemblyDir = Path.GetDirectoryName(
            typeof(NotificationsDbContextDesignTimeFactory).Assembly.Location
        );
        if (!string.IsNullOrEmpty(assemblyDir))
            yield return assemblyDir;
    }
}
