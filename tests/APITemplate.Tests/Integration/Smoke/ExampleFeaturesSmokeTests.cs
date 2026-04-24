using System.Net;
using System.Net.Http.Json;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
[Trait("Docker", "true")]
public sealed class ExampleFeaturesSmokeTests : SmokeTestBase
{
    public ExampleFeaturesSmokeTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    protected override string UsernamePrefix => "smoke-example";

    [Fact]
    public async Task SubmitJob_ReturnsAccepted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Examples.Execute]);
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "data-export" },
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task SseStream_ReturnsEventStreamContentType()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticateAsSeededUser([Permission.Examples.Read]);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sse/stream?count=1");
        HttpResponseMessage response = await Client.SendAsync(
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
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/api/v1/webhooks",
            new { },
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
