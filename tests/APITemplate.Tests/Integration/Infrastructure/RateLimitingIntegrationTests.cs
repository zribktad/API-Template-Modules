using System.Net;
using System.Net.Http.Headers;
using APITemplate.Tests.Integration.Helpers;
using SharedKernel.Application.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public class RateLimitingIntegrationTests : IntegrationTestBase<RateLimitWebApplicationFactory>
{
    public RateLimitingIntegrationTests(RateLimitWebApplicationFactory factory)
        : base(factory) { }

    private const string Global1Path = "/test/rate-limit/no-policy-1";
    private const string Global2Path = "/test/rate-limit/no-policy-2";
    private const string Fixed1Path = "/test/rate-limit/fixed-1";
    private const string Fixed2Path = "/test/rate-limit/fixed-2";
    private const string Sliding1Path = "/test/rate-limit/sliding-1";

    [Fact]
    public async Task GlobalLimiter_Should_Return429_When_LimitExceeded_ForAnonymousIp()
    {
        // Arrange
        const string ip = "80.0.0.1";
        using var client = CreateClientWithIp(ip);

        // Act & Assert
        await ConsumeLimitAsync(client, Global1Path, 2);

        var rejectedResponse = await client.GetAsync(Global1Path, Ct);
        rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GlobalLimiter_Should_PartitionByIp_ForAnonymousUsers()
    {
        // Arrange
        const string ip1 = "80.0.0.2";
        const string ip2 = "80.0.0.3";
        using var client1 = CreateClientWithIp(ip1);
        using var client2 = CreateClientWithIp(ip2);

        // Act
        await ConsumeLimitAsync(client1, Global2Path, 2);

        // Assert
        (await client1.GetAsync(Global2Path, Ct)).StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests
        );
        (await client2.GetAsync(Global2Path, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GlobalLimiter_Should_PartitionByUser_WhenAuthenticated()
    {
        // Arrange
        var user1Token = IntegrationAuthHelper.CreateTestToken(
            userId: Guid.NewGuid(),
            username: "u1"
        );
        var user2Token = IntegrationAuthHelper.CreateTestToken(
            userId: Guid.NewGuid(),
            username: "u2"
        );

        // We use unique IPs as well to ensure total isolation in the test environment
        using var client1 = CreateClientWithIp("80.0.0.4");
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            user1Token
        );

        using var client2 = CreateClientWithIp("80.0.0.5");
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            user2Token
        );

        // Act
        await ConsumeLimitAsync(client1, Global1Path, 2);

        // Assert
        (await client1.GetAsync(Global1Path, Ct)).StatusCode.ShouldBe(
            HttpStatusCode.TooManyRequests
        );
        (await client2.GetAsync(Global1Path, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FixedPolicy_Should_Return429_When_LimitExceeded()
    {
        // Arrange
        using var client = CreateClientWithIp("80.0.0.6");

        // Act & Assert
        await ConsumeLimitAsync(client, Fixed1Path, 2);

        var rejectedResponse = await client.GetAsync(Fixed1Path, Ct);
        rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejectedResponse.Headers.Contains(RateLimitConstants.Headers.Policy).ShouldBeTrue();
        rejectedResponse
            .Headers.GetValues(RateLimitConstants.Headers.Policy)
            .ShouldContain(RateLimitConstants.Policies.FixedTest1);
    }

    [Fact]
    public async Task SlidingPolicy_Should_Return429_When_LimitExceeded()
    {
        // Arrange
        using var client = CreateClientWithIp("80.0.0.7");

        // Act & Assert
        await ConsumeLimitAsync(client, Sliding1Path, 2);

        var rejectedResponse = await client.GetAsync(Sliding1Path, Ct);
        rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejectedResponse.Headers.Contains(RateLimitConstants.Headers.Policy).ShouldBeTrue();
        rejectedResponse
            .Headers.GetValues(RateLimitConstants.Headers.Policy)
            .ShouldContain(RateLimitConstants.Policies.SlidingTest1);
    }

    [Fact]
    public async Task OnRejected_Should_IncludeCorrectHeaderValues()
    {
        // Arrange
        using var client = CreateClientWithIp("80.0.0.10");
        const string path = Fixed1Path; // Fixed policy limit is 2 in RateLimitWebApplicationFactory

        // Act
        await ConsumeLimitAsync(client, path, 2);
        var response = await client.GetAsync(path, Ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Policy header
        response
            .Headers.GetValues(RateLimitConstants.Headers.Policy)
            .ShouldContain(RateLimitConstants.Policies.FixedTest1);

        // Limit header (should match configured limit of 2)
        response.Headers.GetValues(RateLimitConstants.Headers.Limit).ShouldContain("2");

        // Retry-After header
        response.Headers.RetryAfter.ShouldNotBeNull();
        response.Headers.RetryAfter.Delta.HasValue.ShouldBeTrue();
        response.Headers.RetryAfter.Delta.Value.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GlobalLimiter_Should_ApplyEvenToEndpointsWithoutExplicitPolicy()
    {
        // Arrange
        using var client = CreateClientWithIp("80.0.0.9");
        const string path = Global2Path;

        // Act & Assert
        await ConsumeLimitAsync(client, path, 2);

        var response = await client.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response
            .Headers.GetValues(RateLimitConstants.Headers.Policy)
            .ShouldContain(RateLimitConstants.GlobalPolicy);
    }

    private async Task ConsumeLimitAsync(HttpClient client, string path, int limit)
    {
        for (int i = 0; i < limit; i++)
        {
            var response = await client.GetAsync(path, Ct);
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"Request {i + 1} to {path} should be allowed but was {response.StatusCode}"
            );
        }
    }

    private HttpClient CreateClientWithIp(string ip)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(RateLimitWebApplicationFactory.TestIpHeader, ip);
        return client;
    }
}
