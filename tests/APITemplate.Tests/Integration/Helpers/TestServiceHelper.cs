using Identity.Auth.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Application.Options.Infrastructure;
using SharedKernel.Application.Startup;
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
                options.MapInboundClaims = false;
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

    internal static void ReplaceDistributedCacheWithInMemory(IServiceCollection services)
    {
        services.RemoveAll<IDistributedCache>();
        services.AddDistributedMemoryCache();
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
}
