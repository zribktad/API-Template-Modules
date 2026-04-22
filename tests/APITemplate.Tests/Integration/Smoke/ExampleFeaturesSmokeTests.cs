using System.Net;
using System.Text;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class ExampleFeaturesSmokeTests
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient? _client;

    private HttpClient Client => _client ??= _factory.CreateClient();

    public ExampleFeaturesSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SubmitJob_ReturnsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Examples.Execute]);
        var content = new StringContent(
            """{"JobType":"smoke"}""",
            Encoding.UTF8,
            "application/json"
        );
        var response = await Client.PostAsync("/api/v1/jobs", content, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task SseStream_ReturnsEventStreamContentType()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(Client, permissions: [Permission.Examples.Read]);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream?count=1");
        var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");
    }

    [Fact]
    public async Task Webhooks_MissingSignature_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/v1/webhooks", content, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
