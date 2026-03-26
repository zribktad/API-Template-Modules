using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Products;

public class CategoriesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CategoriesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        // 1. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/categories", ct);
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var allCategories = await getAllResponse.Content.ReadFromJsonAsync<
            PagedResponse<CategoryResponse>
        >(TestJsonOptions.CaseInsensitive, ct);
        allCategories.ShouldNotBeNull();
        allCategories!.Items.ShouldBeEmpty();

        // 2. Create category
        var categoryName = $"Electronics-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[] { new { Name = categoryName, Description = "Electronic devices" } },
            },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);

        var createResult = JsonSerializer.Deserialize<BatchResponse>(
            createBody,
            TestJsonOptions.CaseInsensitive
        );
        createResult.ShouldNotBeNull();
        createResult!.Failures.ShouldBeEmpty();
        var createdId = await ResolveCategoryIdAsync(categoryName, ct);

        // 3. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/categories/{createdId}", ct);
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<CategoryResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe(categoryName);

        // 4. Update category
        var updateResponse = await _client.PutAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[]
                {
                    new
                    {
                        Id = createdId,
                        Name = "Updated Electronics",
                        Description = "Updated description",
                    },
                },
            },
            ct
        );

        var updateBody = await updateResponse.Content.ReadAsStringAsync(ct);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, updateBody);

        // 5. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/categories/{createdId}", ct);
        var updated = await verifyResponse.Content.ReadFromJsonAsync<CategoryResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated Electronics");
        updated.Description.ShouldBe("Updated description");

        // 6. Delete category
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { Ids = new[] { createdId } }),
        };
        var deleteResponse = await _client.SendAsync(deleteRequest, ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 7. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/categories/{createdId}", ct);
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CategoryWithoutDescription_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var categoryName = $"Books-{Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = categoryName } } },
            ct
        );

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK, createBody);

        var createResult = JsonSerializer.Deserialize<BatchResponse>(
            createBody,
            TestJsonOptions.CaseInsensitive
        );
        createResult.ShouldNotBeNull();
        createResult!.Failures.ShouldBeEmpty();
        var createdId = await ResolveCategoryIdAsync(categoryName, ct);

        // Verify the created category has no description
        var getResponse = await _client.GetAsync($"/api/v1/categories/{createdId}", ct);
        var created = await getResponse.Content.ReadFromJsonAsync<CategoryResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        created.ShouldNotBeNull();
        created!.Name.ShouldBe(categoryName);
        created.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Create_MultipleCategories_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = "Category A" } } },
            ct
        );
        await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = "Category B" } } },
            ct
        );

        var response = await _client.GetAsync("/api/v1/categories", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        categories.ShouldNotBeNull();
        categories!.Items.Count().ShouldBeGreaterThanOrEqualTo(2);
        categories.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[] { new { Name = "Office Furniture", Description = "Desk and chair" } },
            },
            ct
        );
        await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new
            {
                Items = new[] { new { Name = "Kitchen Tools", Description = "Pans and knives" } },
            },
            ct
        );

        var response = await _client.GetAsync(
            "/api/v1/categories?pageNumber=1&pageSize=1&sortBy=name&sortDirection=asc",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        payload!.Items.Count().ShouldBe(1);
        payload.PageNumber.ShouldBe(1);
        payload.PageSize.ShouldBe(1);
        payload.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsProblemDetailsBody()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        problem.ShouldNotBeNull();
        problem!.Status.ShouldBe((int)HttpStatusCode.NotFound);
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
        problem.ErrorCode.ShouldNotBeNullOrWhiteSpace();
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WithEmptyCategoryName_ReturnsBadRequestWithValidationError()
    {
        // FluentValidationActionFilter validates CreateCategoriesRequest before the handler runs.
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Items = new[] { new { Name = "" } } },
            ct
        );

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain("required");
    }

    [Fact]
    public async Task Delete_NonExistentCategory_ReturnsUnprocessableWithFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var missingId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/categories")
        {
            Content = JsonContent.Create(new { Ids = new[] { missingId } }),
        };
        var response = await _client.SendAsync(request, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, body);

        var batch = await response.Content.ReadFromJsonAsync<BatchResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        batch.ShouldNotBeNull();
        batch!.FailureCount.ShouldBe(1);
        batch.Failures[0].Id.ShouldBe(missingId);
    }

    private async Task<Guid> ResolveCategoryIdAsync(string name, CancellationToken ct)
    {
        var response = await _client.GetAsync(
            $"/api/v1/categories?name={Uri.EscapeDataString(name)}",
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        var item = payload!.Items.FirstOrDefault(c => c.Name == name);
        item.ShouldNotBeNull();
        return item!.Id;
    }
}
