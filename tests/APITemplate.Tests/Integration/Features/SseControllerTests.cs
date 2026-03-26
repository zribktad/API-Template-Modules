using System.Net;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class SseControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SseControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Stream_ReturnsTextEventStreamContentType()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream?count=1");
        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
    }

    [Fact]
    public async Task Stream_ReturnsRequestedNumberOfEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream?count=3");
        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        var dataLines = body.Split('\n').Where(l => l.StartsWith("data:")).ToList();
        dataLines.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Stream_EventsArriveInSseFormat_WithDataPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream?count=2");
        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        var dataLines = body.Split('\n').Where(l => l.StartsWith("data:")).ToList();

        foreach (var line in dataLines)
        {
            line.ShouldStartWith("data: {");
            line.ShouldContain("\"sequence\"");
            line.ShouldContain("\"message\"");
        }
    }

    [Fact]
    public async Task Stream_DefaultCount_ReturnsFiveEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream");
        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        var dataLines = body.Split('\n').Where(l => l.StartsWith("data:")).ToList();
        dataLines.Count.ShouldBe(5);
    }
}
