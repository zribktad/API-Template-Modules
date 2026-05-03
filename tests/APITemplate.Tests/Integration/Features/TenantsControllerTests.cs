using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using BuildingBlocks.Domain.Common;
using BuildingBlocks.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Tenant.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public class TenantsControllerTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _adminTenant = default!;
    private AppUser _adminUser = default!;

    public TenantsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_adminTenant, _adminUser) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "tenants_admin",
            "tenants_admin@test.com",
            ct: ct
        );
        Authenticate();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Unique per test-class instance → no cross-test collisions even with shared DB
    private string Code(string prefix) => $"{prefix}-{_adminTenant.Id:N}"[..20];

    private void Authenticate(Guid? tenantId = null) =>
        IntegrationAuthHelper.Authenticate(
            _client,
            userId: _adminUser.Id,
            tenantId: tenantId ?? _adminTenant.Id,
            username: _adminUser.Username.Value,
            role: "PlatformAdmin",
            permissions:
            [
                Permission.Tenants.Read,
                Permission.Tenants.Create,
                Permission.Tenants.Delete,
            ],
            email: _adminUser.Email.Value,
            subject: _adminUser.KeycloakUserId
        );

    private async Task<TenantResponse> CreateTenantAsync(
        string code,
        string name,
        CancellationToken ct
    )
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Code = code, Name = name },
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TenantResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        return created!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithCorrectData()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = Code("CR");

        var created = await CreateTenantAsync(code, "Acme Corp", ct);

        created.Id.ShouldNotBe(Guid.Empty);
        created.Code.ShouldBe(code);
        created.Name.ShouldBe("Acme Corp");
        created.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetById_ExistingTenant_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("GB"), "Get By Id Corp", ct);

        var response = await _client.GetAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await response.Content.ReadFromJsonAsync<TenantResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.Code.ShouldBe(created.Code);
    }

    [Fact]
    public async Task GetAll_ContainsCreatedTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("GA"), "Get All Corp", ct);

        var response = await _client.GetAsync("/api/v1/tenants", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Delete_ExistingTenant_ReturnsNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("DEL"), "Delete Corp", ct);

        var response = await _client.DeleteAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingTenant_ThenGetById_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("DN"), "Delete NotFound Corp", ct);

        await _client.DeleteAsync($"/api/v1/tenants/{created.Id}", ct);
        var response = await _client.GetAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = Code("DUP");

        await CreateTenantAsync(code, "First", ct);
        var duplicate = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Code = code, Name = "Second" },
            ct
        );

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        HttpValidationProblemDetails? problem =
            await duplicate.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(
                TestJsonOptions.CaseInsensitive,
                ct
            );
        problem.ShouldNotBeNull();
        ExtractErrorCode(problem!).ShouldBe("TNT-0409-CODE");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync($"/api/v1/tenants/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTenantAsync(Code("P1"), "Paged Tenant A", ct);
        await CreateTenantAsync(Code("P2"), "Paged Tenant B", ct);

        var response = await _client.GetAsync(
            "/api/v1/tenants?pageNumber=1&pageSize=1&sortBy=Code&sortDirection=asc",
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
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
    public async Task Create_MultipleTenants_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await CreateTenantAsync(Code("MA"), "Tenant A", ct);
        var b = await CreateTenantAsync(Code("MB"), "Tenant B", ct);

        var response = await _client.GetAsync("/api/v1/tenants", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == a.Id);
        tenants.Items.ShouldContain(t => t.Id == b.Id);
    }

    [Fact]
    public async Task GetAll_DifferentPages_ReturnDisjointItems()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await CreateTenantAsync(Code("DP1"), "Disjoint A", ct);
        var b = await CreateTenantAsync(Code("DP2"), "Disjoint B", ct);
        var c = await CreateTenantAsync(Code("DP3"), "Disjoint C", ct);

        var page1Response = await _client.GetAsync(
            "/api/v1/tenants?pageNumber=1&pageSize=2&sortBy=Code&sortDirection=asc",
            ct
        );
        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var page2Response = await _client.GetAsync(
            "/api/v1/tenants?pageNumber=2&pageSize=2&sortBy=Code&sortDirection=asc",
            ct
        );
        var page2 = await page2Response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        page1.ShouldNotBeNull();
        page2.ShouldNotBeNull();

        page1!.PageNumber.ShouldBe(1);
        page2!.PageNumber.ShouldBe(2);
        page1.TotalCount.ShouldBe(page2.TotalCount);

        // Pages must not share any items
        var page1Ids = page1.Items.Select(t => t.Id).ToHashSet();
        var page2Ids = page2.Items.Select(t => t.Id).ToHashSet();
        page1Ids.Overlaps(page2Ids).ShouldBeFalse();
    }

    [Fact]
    public async Task GetAll_PageSizeCoversAll_ReturnsSinglePage()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTenantAsync(Code("SP1"), "Single Page A", ct);
        await CreateTenantAsync(Code("SP2"), "Single Page B", ct);

        var response = await _client.GetAsync("/api/v1/tenants?pageNumber=1&pageSize=100", ct);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        payload!.Items.Count().ShouldBe(payload.TotalCount);
        payload.HasNextPage.ShouldBeFalse();
        payload.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task GetAll_CallerFromDifferentTenant_SeesAllTenants()
    {
        var ct = TestContext.Current.CancellationToken;

        var created = await CreateTenantAsync(Code("CT"), "Cross Tenant Corp", ct);

        // Switch to a completely different tenant context (same user, different tenantId claim)
        var otherTenantId = Guid.NewGuid();
        Authenticate(otherTenantId);

        var response = await _client.GetAsync("/api/v1/tenants?pageSize=100", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);

        // Restore original auth for other tests
        Authenticate();
    }

    [Fact]
    public async Task GetAll_PageOutOfRange_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTenantAsync(Code("OOR"), "Out Of Range Corp", ct);

        var response = await _client.GetAsync("/api/v1/tenants?pageNumber=9999&pageSize=1", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_SearchByCode_ReturnsMatchingTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = Code("FTS");
        var created = await CreateTenantAsync(code, "Search By Code Corp", ct);

        var response = await _client.GetAsync($"/api/v1/tenants?query={code}&pageSize=100", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task GetAll_SearchByName_ReturnsMatchingTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var uniqueName = $"UniqueName{_adminTenant.Id:N}"[..30];
        var created = await CreateTenantAsync(Code("NM"), uniqueName, ct);

        var response = await _client.GetAsync(
            $"/api/v1/tenants?query={Uri.EscapeDataString(uniqueName)}&pageSize=100",
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);
    }

    private static string ExtractErrorCode(HttpValidationProblemDetails problem)
    {
        return
            problem.Extensions.TryGetValue("errorCode", out object? code) && code is JsonElement je
            ? je.GetString() ?? string.Empty
            : code?.ToString() ?? string.Empty;
    }
}
