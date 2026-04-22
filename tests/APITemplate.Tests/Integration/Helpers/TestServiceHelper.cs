using BackgroundJobs.TickerQ;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Application.BackgroundJobs;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Application.Startup;
using SharedKernel.Infrastructure.Health;
using StackExchange.Redis;

namespace APITemplate.Tests.Integration.Helpers;

internal static class TestServiceHelper
{
    internal static void ConfigureTestAuthentication(IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "http://localhost:8180/realms/api-template",
                    ValidateAudience = true,
                    ValidAudience = "api-template",
                    ValidateLifetime = true,
                    IssuerSigningKey = IntegrationAuthHelper.SecurityKey,
                    ValidateIssuerSigningKey = true,
                };
            }
        );

        services.PostConfigure<OpenIdConnectOptions>(
            AuthConstants.BffSchemes.Oidc,
            options =>
            {
                options.MetadataAddress = null;
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "http://localhost:8180/realms/api-template",
                    AuthorizationEndpoint =
                        "http://localhost:8180/realms/api-template/protocol/openid-connect/auth",
                    TokenEndpoint =
                        "http://localhost:8180/realms/api-template/protocol/openid-connect/token",
                    EndSessionEndpoint =
                        "http://localhost:8180/realms/api-template/protocol/openid-connect/logout",
                    UserInfoEndpoint =
                        "http://localhost:8180/realms/api-template/protocol/openid-connect/userinfo",
                };
            }
        );
    }

    internal static void RemoveExternalHealthChecks(IServiceCollection services)
    {
        services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(
            options =>
            {
                List<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration> toRemove =
                    options
                        .Registrations.Where(r =>
                            r.Name
                                is HealthCheckNames.MongoDb
                                    or HealthCheckNames.Keycloak
                                    or HealthCheckNames.PostgreSql
                                    or HealthCheckNames.Redis
                                    or HealthCheckNames.Smtp
                                    or HealthCheckNames.WolverineMessageStore
                                    or HealthCheckNames.WolverineDeadLetters
                                    or HealthCheckNames.OtlpCollector
                        )
                        .ToList();
                foreach (var r in toRemove)
                    options.Registrations.Remove(r);
            }
        );
    }

    internal static void ReplaceOutputCacheWithInMemory(IServiceCollection services)
    {
        services.RemoveAll<IOutputCacheStore>();
        services.RemoveAll<IConnectionMultiplexer>();
        services.AddSingleton<IConnectionMultiplexer>(
            new Moq.Mock<IConnectionMultiplexer>().Object
        );
        services.AddOutputCache();
        services.RemoveAll<IOutputCacheStore>();
        services.AddSingleton<IOutputCacheStore, TestOutputCacheStore>();
        services.RemoveAll<IValidateOptions<RedisOptions>>();
        services.RemoveAll<IOptionsChangeTokenSource<RedisOptions>>();
    }

    internal static void ReplaceDataProtectionWithInMemory(IServiceCollection services)
    {
        services.RemoveAll<IDataProtectionProvider>();
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
    }

    internal static void RemoveTickerQRuntimeServices(IServiceCollection services)
    {
        List<ServiceDescriptor> runtimeDescriptors = services
            .Where(IsTickerQRuntimeDescriptor)
            .ToList();
        foreach (ServiceDescriptor descriptor in runtimeDescriptors)
            services.Remove(descriptor);
    }

    internal static void ReplaceStartupCoordinationWithNoOp(IServiceCollection services)
    {
        services.RemoveAll<IStartupTaskCoordinator>();
        services.RemoveAll<APITemplate.Tests.Helpers.TestNoOpStartupTaskCoordinator>();
        services.AddScoped<APITemplate.Tests.Helpers.TestNoOpStartupTaskCoordinator>();
        services.AddScoped<IStartupTaskCoordinator>(sp =>
            sp.GetRequiredService<APITemplate.Tests.Helpers.TestNoOpStartupTaskCoordinator>()
        );
    }

    private static bool IsTickerQRuntimeDescriptor(ServiceDescriptor descriptor) =>
        IsTickerQRuntimeType(descriptor.ServiceType)
        || IsTickerQRuntimeType(descriptor.ImplementationType)
        || IsTickerQRuntimeInstance(descriptor.ImplementationInstance)
        || IsTickerQRuntimeFactory(descriptor.ImplementationFactory)
        || descriptor.ServiceType == typeof(TickerQSchedulerDbContext)
        || descriptor.ServiceType == typeof(TickerQRecurringJobRegistrar)
        || descriptor.ServiceType == typeof(IDistributedJobCoordinator)
        || (
            descriptor.ServiceType == typeof(IRecurringBackgroundJobRegistration)
            && IsTickerQRuntimeType(descriptor.ImplementationType)
        );

    private static bool IsTickerQRuntimeFactory(
        Func<IServiceProvider, object>? implementationFactory
    ) =>
        implementationFactory?.Method.DeclaringType is { } declaringType
        && IsTickerQRuntimeType(declaringType);

    private static bool IsTickerQRuntimeInstance(object? implementationInstance) =>
        implementationInstance is not null
        && IsTickerQRuntimeType(implementationInstance.GetType());

    private static bool IsTickerQRuntimeType(Type? type)
    {
        if (type is null)
            return false;

        if (
            IsTickerQRuntimeNamespace(type.Namespace)
            || IsTickerQRuntimeAssembly(type.Assembly.GetName().Name)
        )
            return true;

        if (!type.IsGenericType)
            return false;

        return type.GetGenericArguments().Any(IsTickerQRuntimeType);
    }

    private static bool IsTickerQRuntimeNamespace(string? typeNamespace) =>
        typeNamespace is not null
        && (
            typeNamespace.StartsWith("TickerQ", StringComparison.Ordinal)
            || typeNamespace.Contains(".BackgroundJobs.TickerQ", StringComparison.Ordinal)
        );

    private static bool IsTickerQRuntimeAssembly(string? assemblyName) =>
        assemblyName is not null && assemblyName.StartsWith("TickerQ", StringComparison.Ordinal);
}
