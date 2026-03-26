using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

public sealed class RateLimitingWebApplicationFactory : CustomWebApplicationFactory
{
    public const int PermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.Configure<RateLimitingOptions>(o =>
            {
                o.PermitLimit = PermitLimit;
                o.WindowMinutes = 1;
            }));
    }
}

public class RateLimitingTests : IClassFixture<RateLimitingWebApplicationFactory>
{
    private readonly RateLimitingWebApplicationFactory _factory;
    private const int PermitLimit = RateLimitingWebApplicationFactory.PermitLimit;

    public RateLimitingTests(RateLimitingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExceedingLimit_SameUser_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client, username: "rl-exceed-user");

        for (var i = 0; i < PermitLimit; i++)
            (await client.GetAsync("/api/v1/products", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var throttled = await client.GetAsync("/api/v1/products", ct);
        throttled.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task WithinLimit_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client, username: "rl-within-user");

        for (var i = 0; i < PermitLimit; i++)
            (await client.GetAsync("/api/v1/products", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DifferentUsers_HaveIndependentBuckets()
    {
        var ct = TestContext.Current.CancellationToken;
        var clientA = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(clientA, username: "rl-user-a");

        var clientB = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(clientB, username: "rl-user-b");

        // Exhaust user-a's bucket
        for (var i = 0; i < PermitLimit; i++)
            await clientA.GetAsync("/api/v1/products", ct);

        (await clientA.GetAsync("/api/v1/products", ct)).StatusCode
            .ShouldBe(HttpStatusCode.TooManyRequests);

        // user-b has their own independent bucket — not affected
        (await clientB.GetAsync("/api/v1/products", ct)).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }
}
