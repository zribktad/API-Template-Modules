using System.Net;
using System.Text;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Tests.Unit.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Webhooks.Contracts;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
public sealed class WebhooksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    /// <summary>
    ///     Lazy client: test class ctor can run before the fixture's <see cref="IAsyncLifetime.InitializeAsync" />,
    ///     so we must not call <see cref="WebApplicationFactory{Program}.CreateClient" /> in the ctor.
    /// </summary>
    private HttpClient Client => field ??= _factory.CreateClient();

    public WebhooksControllerTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Receive_ValidSignature_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = """{"eventType":"order.created","eventId":"evt-1","data":{}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = WebhookTestHelper.ComputeHmacSignature(
            body,
            timestamp,
            TestConfigurationHelper.TestWebhookSecret
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookConstants.SignatureHeader, signature);
        request.Headers.Add(WebhookConstants.TimestampHeader, timestamp);

        var response = await Client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Receive_InvalidSignature_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = """{"eventType":"order.created","eventId":"evt-2","data":{}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookConstants.SignatureHeader, "wrong-signature");
        request.Headers.Add(WebhookConstants.TimestampHeader, timestamp);

        var response = await Client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Receive_ExpiredTimestamp_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = """{"eventType":"order.created","eventId":"evt-3","data":{}}""";
        var expiredTimestamp = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 600).ToString(); // 10 minutes ago
        var signature = WebhookTestHelper.ComputeHmacSignature(
            body,
            expiredTimestamp,
            TestConfigurationHelper.TestWebhookSecret
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(WebhookConstants.SignatureHeader, signature);
        request.Headers.Add(WebhookConstants.TimestampHeader, expiredTimestamp);

        var response = await Client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Receive_MissingHeaders_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var body = """{"eventType":"order.created","eventId":"evt-4","data":{}}""";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        var response = await Client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
