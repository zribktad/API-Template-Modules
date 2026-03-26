using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Domain.Enums;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class JobsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JobsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_ValidRequest_Returns202WithLocationHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "data-export" },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task Submit_ValidRequest_ResponseContainsPendingStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "report-generation" },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);

        var result = JsonSerializer.Deserialize<JobStatusResponse>(
            body,
            TestJsonOptions.CaseInsensitive
        );
        result.ShouldNotBeNull();
        result!.Status.ShouldBe(JobStatus.Pending);
        result.JobType.ShouldBe("report-generation");
    }

    [Fact]
    public async Task GetStatus_AfterSubmit_ReturnsJobWithMatchingId()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var submitResponse = await _client.PostAsJsonAsync(
            "/api/v1/jobs",
            new { JobType = "async-task" },
            ct
        );
        var submitBody = await submitResponse.Content.ReadAsStringAsync(ct);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted, submitBody);
        var submitted = JsonSerializer.Deserialize<JobStatusResponse>(
            submitBody,
            TestJsonOptions.CaseInsensitive
        )!;

        var getResponse = await _client.GetAsync($"/api/v1/jobs/{submitted.Id}", ct);
        var getBody = await getResponse.Content.ReadAsStringAsync(ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK, getBody);

        var status = JsonSerializer.Deserialize<JobStatusResponse>(
            getBody,
            TestJsonOptions.CaseInsensitive
        );
        status.ShouldNotBeNull();
        status!.Id.ShouldBe(submitted.Id);
        status.JobType.ShouldBe("async-task");
    }

    [Fact]
    public async Task GetStatus_NonExistentId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_EmptyJobType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync("/api/v1/jobs", new { JobType = "" }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
