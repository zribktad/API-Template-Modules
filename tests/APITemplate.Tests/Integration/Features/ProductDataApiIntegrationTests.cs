using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Identity.Directory.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Features;

[Trait("Category", "Integration")]
[Trait("Docker", "true")]
[Collection(IntegrationCollectionNames.HttpStateful)]
public sealed class ProductDataApiIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private Tenant _tenant = default!;
    private AppUser _user = default!;

    public ProductDataApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    public async ValueTask InitializeAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (_tenant, _user) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            "productdata_admin",
            "productdata_admin@test.com",
            ct: ct
        );
        _client.AuthenticateAs(
            _tenant,
            _user,
            Permission.ProductData.Read,
            Permission.ProductData.Create,
            Permission.ProductData.Delete
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ImageAndVideoCrud_Flow_Works()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        HttpResponseMessage imageCreate = await _client.PostAsJsonAsync(
            "/api/v1/product-data/image",
            new
            {
                Title = $"Image-{Guid.NewGuid():N}",
                Description = "img",
                Width = 640,
                Height = 480,
                Format = "png",
                FileSizeBytes = 512L,
            },
            ct
        );
        await imageCreate.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductDataContractResponse image =
            await imageCreate.ReadJsonAsync<ProductDataContractResponse>(ct);
        image.Type.ShouldBe("image");

        HttpResponseMessage videoCreate = await _client.PostAsJsonAsync(
            "/api/v1/product-data/video",
            new
            {
                Title = $"Video-{Guid.NewGuid():N}",
                Description = "video",
                DurationSeconds = 60,
                Resolution = "1080p",
                Format = "mp4",
                FileSizeBytes = 1024L,
            },
            ct
        );
        await videoCreate.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductDataContractResponse video =
            await videoCreate.ReadJsonAsync<ProductDataContractResponse>(ct);
        video.Type.ShouldBe("video");

        HttpResponseMessage listResponse = await _client.GetAsync("/api/v1/product-data", ct);
        await listResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        IReadOnlyList<ProductDataContractResponse> list = await listResponse.ReadJsonAsync<
            List<ProductDataContractResponse>
        >(ct);
        list.ShouldContain(x => x.Id == image.Id);
        list.ShouldContain(x => x.Id == video.Id);

        HttpResponseMessage byIdResponse = await _client.GetAsync(
            $"/api/v1/product-data/{image.Id}",
            ct
        );
        await byIdResponse.ShouldBeStatusAsync(HttpStatusCode.OK, ct);
        ProductDataContractResponse byId =
            await byIdResponse.ReadJsonAsync<ProductDataContractResponse>(ct);
        byId.Id.ShouldBe(image.Id);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            $"/api/v1/product-data/{image.Id}",
            ct
        );
        await deleteResponse.ShouldBeStatusAsync(HttpStatusCode.NoContent, ct);

        HttpResponseMessage deleted = await _client.GetAsync(
            $"/api/v1/product-data/{image.Id}",
            ct
        );
        await deleted.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task ProductData_WhenMissing_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/product-data/{Guid.NewGuid()}",
            ct
        );
        await response.ShouldBeStatusAsync(HttpStatusCode.NotFound, ct);
    }

    [Fact]
    public async Task CreateImage_WithInvalidPayload_ReturnsValidationFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/product-data/image",
            new
            {
                Title = "",
                Width = 0,
                Height = 0,
                Format = "exe",
                FileSizeBytes = 0L,
            },
            ct
        );

        response.StatusCode.ShouldBeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity
        );
    }

    [Fact]
    public async Task DeleteProductData_WithoutDeletePermission_ReturnsForbidden()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _client.AuthenticateAs(
            _tenant,
            _user,
            Permission.ProductData.Read,
            Permission.ProductData.Create
        );

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/v1/product-data/image",
            new
            {
                Title = $"NoDelete-{Guid.NewGuid():N}",
                Description = "img",
                Width = 320,
                Height = 240,
                Format = "png",
                FileSizeBytes = 300L,
            },
            ct
        );
        await createResponse.ShouldBeStatusAsync(HttpStatusCode.Created, ct);
        ProductDataContractResponse created =
            await createResponse.ReadJsonAsync<ProductDataContractResponse>(ct);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync(
            $"/api/v1/product-data/{created.Id}",
            ct
        );
        await deleteResponse.ShouldBeStatusAsync(HttpStatusCode.Forbidden, ct);
    }
}
