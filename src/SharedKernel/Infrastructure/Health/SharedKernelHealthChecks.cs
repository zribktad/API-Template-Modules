using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Infrastructure.Configuration;

namespace SharedKernel.Infrastructure.Health;

public sealed class SharedKernelHealthChecks : IHealthCheckModule
{
    private readonly IConfiguration _configuration;
    private readonly bool _isDevelopment;

    public SharedKernelHealthChecks(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _isDevelopment = environment.IsDevelopment();
    }

    public void RegisterHealthChecks(IHealthChecksBuilder builder)
    {
        AddPostgreSql(builder);
        AddKeycloak(builder);
        AddRedis(builder);
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

    private void AddRedis(IHealthChecksBuilder builder)
    {
        if (_configuration.IsRedisConfigured())
        {
            RedisOptions redisOptions = _configuration
                .SectionFor<RedisOptions>()
                .Get<RedisOptions>()!;

            builder.AddRedis(
                redisOptions.ConnectionString,
                HealthCheckNames.Redis,
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

        Uri? endpoint = observabilityOptions.ResolveOtlpEndpoint(_isDevelopment);
        if (endpoint is null)
            return;

        builder.Services.Configure<OtlpCollectorHealthCheckOptions>(o =>
            o.Endpoint = endpoint.AbsoluteUri
        );
        builder.AddCheck<OtlpCollectorHealthCheck>(
            HealthCheckNames.OtlpCollector,
            tags: [HealthCheckTags.Ready, HealthCheckTags.External]
        );
    }
}
