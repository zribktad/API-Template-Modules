using System.Net;
using System.Text;
using Identity.Auth.Security;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Smoke;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class ExampleFeaturesSmokeTests : IAsyncLifetime
{
    private const string ServiceAccountUsername =
        $"{AuthConstants.Claims.ServiceAccountUsernamePrefix}smoke-examples";
    private readonly CustomWebApplicationFactory _factory;
    private Guid _tenantId;

    private HttpClient Client => field ??= _factory.CreateClient();

    public ExampleFeaturesSmokeTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username: $"smoke-example-{suffix}",
            email: $"smoke-example-{suffix}@example.com",
            ct: ct
        );
        _tenantId = tenant.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SubmitJob_ReturnsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Examples.Execute]
        );
        var content = new StringContent(
            """{"JobType":"data-export"}""",
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
        IntegrationAuthHelper.Authenticate(
            Client,
            tenantId: _tenantId,
            username: ServiceAccountUsername,
            permissions: [Permission.Examples.Read]
        );
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
