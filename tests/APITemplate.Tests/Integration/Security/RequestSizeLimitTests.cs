using System.Net;
using System.Text;
using APITemplate.Tests.Integration.Helpers;
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
}
