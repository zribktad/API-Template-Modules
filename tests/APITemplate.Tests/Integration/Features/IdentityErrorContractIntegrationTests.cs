using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpRead)]
public sealed class IdentityErrorContractIntegrationTests
{
    private readonly HttpClient _client;

    public IdentityErrorContractIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    [Fact]
    public async Task KeycloakWebhook_WhenDisabled_ReturnsProblemDetailsWithErrorCode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/internal/keycloak-events/password-changed",
            new { keycloakUserId = Guid.NewGuid().ToString() },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        problem.ShouldNotBeNull();
        ExtractErrorCode(problem!).ShouldBe("KC-WH-0404");
        ExtractTraceId(problem!).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TenantInvitationAccept_WithInvalidToken_ReturnsNotFoundProblemDetails()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/tenant-invitations/accept",
            new { token = Guid.NewGuid().ToString("N") },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        problem.ShouldNotBeNull();
        ExtractErrorCode(problem!).ShouldBe("INV-0404");
        ExtractTraceId(problem!).ShouldNotBeNullOrWhiteSpace();
    }

    private static string ExtractErrorCode(ProblemDetails problem)
    {
        return
            problem.Extensions.TryGetValue("errorCode", out object? code) && code is JsonElement je
            ? je.GetString() ?? string.Empty
            : code?.ToString() ?? string.Empty;
    }

    private static string ExtractTraceId(ProblemDetails problem)
    {
        return
            problem.Extensions.TryGetValue("traceId", out object? traceId)
            && traceId is JsonElement je
            ? je.GetString() ?? string.Empty
            : traceId?.ToString() ?? string.Empty;
    }
}
