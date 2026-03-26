using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

public class TenantsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public TenantsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
    }

    // Unique per test-class instance → no cross-test collisions even with shared DB
    private string Code(string prefix) => $"{prefix}-{_tenantId:N}"[..20];

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
            "/api/v1/tenants?pageNumber=1&pageSize=1&sortBy=code&sortDirection=asc",
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
            "/api/v1/tenants?pageNumber=1&pageSize=2&sortBy=code&sortDirection=asc",
            ct
        );
        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );

        var page2Response = await _client.GetAsync(
            "/api/v1/tenants?pageNumber=2&pageSize=2&sortBy=code&sortDirection=asc",
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

        // Create a tenant while authenticated as _tenantId
        var created = await CreateTenantAsync(Code("CT"), "Cross Tenant Corp", ct);

        // Switch to a completely different tenant context
        var otherTenantId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client, tenantId: otherTenantId);

        var response = await _client.GetAsync("/api/v1/tenants?pageSize=100", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);

        // Restore original auth for other tests
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
    }

    [Fact]
    public async Task GetAll_PageOutOfRange_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTenantAsync(Code("OOR"), "Out Of Range Corp", ct);

        var response = await _client.GetAsync("/api/v1/tenants?pageNumber=9999&pageSize=1", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
