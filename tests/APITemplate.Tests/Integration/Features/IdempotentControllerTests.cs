using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Application.Features.Examples.DTOs;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class IdempotentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IdempotentControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_WithIdempotencyKey_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/idempotent")
        {
            Content = JsonContent.Create(new { Name = "Idempotent Test", Description = "Desc" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task Post_SameKeySamePayload_ReturnsCachedResponse_SameId()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);
        var key = Guid.NewGuid().ToString();

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/idempotent")
        {
            Content = JsonContent.Create(new { Name = "Idempotent Dup", Description = "First" }),
        };
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1, ct);
        var body1 = await response1.Content.ReadAsStringAsync(ct);
        response1.StatusCode.ShouldBe(HttpStatusCode.Created, body1);
        var result1 = JsonSerializer.Deserialize<IdempotentCreateResponse>(
            body1,
            TestJsonOptions.CaseInsensitive
        )!;

        // Second request with same key
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/idempotent")
        {
            Content = JsonContent.Create(new { Name = "Idempotent Dup", Description = "First" }),
        };
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2, ct);
        var body2 = await response2.Content.ReadAsStringAsync(ct);

        var result2 = JsonSerializer.Deserialize<IdempotentCreateResponse>(
            body2,
            TestJsonOptions.CaseInsensitive
        )!;
        result2.Id.ShouldBe(result1.Id);
    }

    [Fact]
    public async Task Post_DifferentKey_CreatesNewResource()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/idempotent")
        {
            Content = JsonContent.Create(new { Name = "Resource A", Description = (string?)null }),
        };
        request1.Headers.Add("Idempotency-Key", "key-a-" + Guid.NewGuid());
        var response1 = await _client.SendAsync(request1, ct);
        var body1 = await response1.Content.ReadAsStringAsync(ct);
        var result1 = JsonSerializer.Deserialize<IdempotentCreateResponse>(
            body1,
            TestJsonOptions.CaseInsensitive
        )!;

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/idempotent")
        {
            Content = JsonContent.Create(new { Name = "Resource B", Description = (string?)null }),
        };
        request2.Headers.Add("Idempotency-Key", "key-b-" + Guid.NewGuid());
        var response2 = await _client.SendAsync(request2, ct);
        var body2 = await response2.Content.ReadAsStringAsync(ct);
        var result2 = JsonSerializer.Deserialize<IdempotentCreateResponse>(
            body2,
            TestJsonOptions.CaseInsensitive
        )!;

        result1.Id.ShouldNotBe(result2.Id);
    }

    [Fact]
    public async Task Post_WithoutIdempotencyKey_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/idempotent",
            new { Name = "No Key 1", Description = (string?)null },
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
