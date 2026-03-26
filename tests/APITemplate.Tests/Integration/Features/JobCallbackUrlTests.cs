using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class JobCallbackUrlTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JobCallbackUrlTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_WithCallbackUrl_PersistsAndReturnsCallbackUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        const string callbackUrl = "https://example.com/webhook/callback";

        var submitResponse = await _client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "callback-test", CallbackUrl = callbackUrl },
            ct
        );

        var submitBody = await submitResponse.Content.ReadAsStringAsync(ct);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, submitBody);

        var submitted = JsonSerializer.Deserialize<JobStatusResponse>(
            submitBody,
            TestJsonOptions.CaseInsensitive
        )!;
        submitted.CallbackUrl.ShouldBe(callbackUrl);

        var getResponse = await _client.GetAsync($"/api/v1/jobs/{submitted.Id}", ct);
        var getBody = await getResponse.Content.ReadAsStringAsync(ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, getBody);

        var status = JsonSerializer.Deserialize<JobStatusResponse>(
            getBody,
            TestJsonOptions.CaseInsensitive
        )!;
        status.CallbackUrl.ShouldBe(callbackUrl);
    }

    [Fact]
    public async Task Submit_WithoutCallbackUrl_ReturnsNullCallbackUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var submitResponse = await _client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "no-callback-test" },
            ct
        );

        var submitBody = await submitResponse.Content.ReadAsStringAsync(ct);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, submitBody);

        var submitted = JsonSerializer.Deserialize<JobStatusResponse>(
            submitBody,
            TestJsonOptions.CaseInsensitive
        )!;
        submitted.CallbackUrl.ShouldBeNull();
    }
}
