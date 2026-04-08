using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Infrastructure.Configuration;

namespace SharedKernel.Infrastructure.Health;

public sealed class SharedKernelHealthChecks : IHealthCheckModule
{
    private readonly IConfiguration _configuration;

    public SharedKernelHealthChecks(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RegisterHealthChecks(IHealthChecksBuilder builder)
    {
        AddPostgreSql(builder);
        AddKeycloak(builder);
        AddDragonfly(builder);
    }

    private void AddPostgreSql(IHealthChecksBuilder builder)
    {
        string connectionString =
            _configuration.GetConnectionString(ConfigurationSections.DefaultConnection)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConfigurationSections.DefaultConnection}' is not configured."
            );

        builder.AddNpgSql(
            connectionString,
            name: HealthCheckNames.PostgreSql,
            tags: [HealthCheckTags.Ready, HealthCheckTags.Database]
        );
    }

    private void AddKeycloak(IHealthChecksBuilder builder)
    {
        builder.Services.AddValidatedOptions<KeycloakHealthCheckOptions>(_configuration);
        builder.AddCheck<KeycloakHealthCheck>(
            HealthCheckNames.Keycloak,
            tags: [HealthCheckTags.Ready, HealthCheckTags.External]
        );
    }

    private void AddDragonfly(IHealthChecksBuilder builder)
    {
        DragonflyOptions? dragonflyOptions = _configuration
            .SectionFor<DragonflyOptions>()
            .Get<DragonflyOptions>();

        if (
            dragonflyOptions is not null
            && !string.IsNullOrWhiteSpace(dragonflyOptions.ConnectionString)
        )
        {
            builder.AddRedis(
                dragonflyOptions.ConnectionString,
                HealthCheckNames.Dragonfly,
                tags: [HealthCheckTags.Ready, HealthCheckTags.Cache]
            );
        }
    }
}
