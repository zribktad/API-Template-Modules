using System.Net;
using System.Text;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class WebhooksControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebhooksControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

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

        var response = await _client.SendAsync(request, ct);
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

        var response = await _client.SendAsync(request, ct);
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

        var response = await _client.SendAsync(request, ct);
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
        // No signature or timestamp headers

        var response = await _client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
