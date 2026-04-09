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
        AddWolverineMessageStore(builder);
        AddWolverineDeadLetters(builder);
        AddOtlpCollector(builder);
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

    private void AddWolverineMessageStore(IHealthChecksBuilder builder)
    {
        builder.Services.AddValidatedOptions<WolverineHealthCheckOptions>(_configuration);
        builder.AddCheck<WolverineMessageStoreHealthCheck>(
            HealthCheckNames.WolverineMessageStore,
            tags: [HealthCheckTags.Ready, HealthCheckTags.Messaging]
        );
    }

    private void AddWolverineDeadLetters(IHealthChecksBuilder builder)
    {
        builder.AddCheck<WolverineDeadLetterHealthCheck>(
            HealthCheckNames.WolverineDeadLetters,
            tags: [HealthCheckTags.Ready, HealthCheckTags.Messaging]
        );
    }

    private void AddOtlpCollector(IHealthChecksBuilder builder)
    {
        ObservabilityOptions? observabilityOptions = _configuration
            .SectionFor<ObservabilityOptions>()
            .Get<ObservabilityOptions>();

        if (observabilityOptions is null)
            return;

        string? endpoint = ResolveOtlpEndpoint(observabilityOptions);
        if (endpoint is null)
            return;

        builder.Services.Configure<OtlpCollectorHealthCheckOptions>(o => o.Endpoint = endpoint);
        builder.AddCheck<OtlpCollectorHealthCheck>(
            HealthCheckNames.OtlpCollector,
            tags: [HealthCheckTags.Ready, HealthCheckTags.External]
        );
    }

    private static string? ResolveOtlpEndpoint(ObservabilityOptions options)
    {
        if (
            options.Exporters.Otlp.Enabled == true
            && !string.IsNullOrWhiteSpace(options.Otlp.Endpoint)
        )
            return options.Otlp.Endpoint;

        if (
            options.Exporters.Aspire.Enabled == true
            && !string.IsNullOrWhiteSpace(options.Aspire.Endpoint)
        )
            return options.Aspire.Endpoint;

        return null;
    }
}
