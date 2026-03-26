using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Application.Common.DTOs;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class BatchControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BatchControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProducts_AllValid_CreatesAllAndReturnsIds()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new
        {
            Items = new[]
            {
                new
                {
                    Name = "Batch Product 1",
                    Description = "Desc 1",
                    Price = 10.00m,
                },
                new
                {
                    Name = "Batch Product 2",
                    Description = "Desc 2",
                    Price = 20.00m,
                },
                new
                {
                    Name = "Batch Product 3",
                    Description = "Desc 3",
                    Price = 30.00m,
                },
            },
        };

        var response = await _client.PostAsJsonAsync("/api/v1/products", request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var result = JsonSerializer.Deserialize<BatchResponse>(
            body,
            TestJsonOptions.CaseInsensitive
        );
        result.ShouldNotBeNull();
        result!.SuccessCount.ShouldBe(3);
        result.FailureCount.ShouldBe(0);
        result.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateProducts_OneInvalid_ReturnsPerItemErrors_NothingPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        // Item[1] has price > 1000 but no description — fails the cross-field FluentValidation rule
        // in BatchProductItemValidator (not DataAnnotations), so it reaches the handler's per-item validation
        var request = new
        {
            Items = new object[]
            {
                new
                {
                    Name = "Valid Product",
                    Description = "Desc",
                    Price = 10.00m,
                },
                new
                {
                    Name = "Expensive No Desc",
                    Description = (string?)null,
                    Price = 1500.00m,
                },
                new
                {
                    Name = "Also Valid",
                    Description = "Desc",
                    Price = 30.00m,
                },
            },
        };

        var response = await _client.PostAsJsonAsync("/api/v1/products", request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, body);

        var result = JsonSerializer.Deserialize<BatchResponse>(
            body,
            TestJsonOptions.CaseInsensitive
        );
        result.ShouldNotBeNull();
        result!.FailureCount.ShouldBeGreaterThan(0);
        result.Failures.ShouldContain(f => f.Index == 1);
        result.Failures.First(f => f.Index == 1).Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateProducts_EmptyList_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var request = new { Items = Array.Empty<object>() };
        var response = await _client.PostAsJsonAsync("/api/v1/products", request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
