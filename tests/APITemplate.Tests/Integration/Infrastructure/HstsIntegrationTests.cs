using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpRead)]
public sealed class HstsIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public HstsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task StrictTransportSecurity_Header_Presence_ShouldMatchEnvironment(
        string environment,
        bool expectHeader
    )
    {
        // Arrange
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);

            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HstsOptions>(options =>
                {
                    // HSTS middleware excludes loopback/localhost by default.
                    // We clear it here so we can verify the header presence via WebApplicationFactory.
                    options.ExcludedHosts.Clear();
                });

                // Prepend middleware to spoof RemoteIpAddress (HSTS skips loopback IPs).
                services.AddSingleton<IStartupFilter, SpoofRemoteIpStartupFilter>();
            });
        });

        var client = testFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );

        // Act
        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.Headers.Contains(HeaderNames.StrictTransportSecurity).ShouldBe(expectHeader);
    }

    private sealed class SpoofRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(
                    (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse("1.1.1.1");
                        return nextMiddleware();
                    }
                );
                next(app);
            };
        }
    }
}
