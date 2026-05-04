using System.Net;
using System.Text;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Tests.Integration.Infrastructure;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Security;

[Trait("Category", "Integration")]
[Trait("Category", "Integration.Docker")]
[Trait("Docker", "true")]
public sealed class RequestSizeLimitTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RequestSizeLimitTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRequest_WhenBodyExceedsLimit_ShouldReturnRequestEntityTooLarge()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        // Send a POST request to an anonymous endpoint with a payload larger than the 1MB limit.
        // 1MB = 1,048,576 bytes. 1.2M chars will be larger than 1MB.
        var largePayload = new string('a', 1200000);
        var content = new StringContent(
            $"{{\"name\": \"{largePayload}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        // We use /health/ready which is [AllowAnonymous].
        var response = await client.PostAsync("/health/ready", content, ct);

        // Assert
        // The global UseRequestSizeLimits middleware or the server itself should return 413.
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task PostRequest_WhenEndpointHasDisableRequestSizeLimit_ShouldNotReturn413()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        // Send a POST request with a payload larger than the global 1MB limit
        // to an endpoint decorated with [DisableRequestSizeLimit].
        // The endpoint metadata should be respected and the request should succeed.
        var largePayload = new string('a', 1200000);
        var content = new StringContent(
            $"{{\"name\": \"{largePayload}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/test/request-size/disable-limit", content, ct);

        // Assert
        // The middleware should skip the global limit because the endpoint has [DisableRequestSizeLimit].
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostRequest_WhenEndpointHasCustomLimit_ShouldNotReturn413()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        // Send a POST request with a payload larger than the global 1MB limit
        // but within the custom 10MB limit set by [RequestSizeLimit(10 * 1024 * 1024)].
        var largePayload = new string('a', 1200000); // ~1.2MB, exceeds global but under custom 10MB
        var content = new StringContent(
            $"{{\"name\": \"{largePayload}\"}}",
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("/test/request-size/custom-limit", content, ct);

        // Assert
        // The middleware should skip the global limit because the endpoint has its own [RequestSizeLimit].
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostRequest_WhenEndpointHasGlobalLimitAndPayloadIsSmall_ShouldReturnOK()
    {
        // Arrange
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        // Send a small payload to an endpoint that uses the global limit.
        var content = new StringContent("{\"name\": \"small\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/test/request-size/global-limit", content, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
