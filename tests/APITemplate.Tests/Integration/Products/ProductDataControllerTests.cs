using System.Net;
using System.Net.Http.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Products;

public class ProductDataControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IProductDataRepository> _repositoryMock;

    public ProductDataControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _repositoryMock = factory.Services.GetRequiredService<Mock<IProductDataRepository>>();
        _repositoryMock.Reset();
    }

    [Fact]
    public async Task GetAll_WithToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _client.GetAsync("/api/v1/product-data", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_WithTypeFilter_PassesTypeToRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ImageProductData
                {
                    Title = "Photo",
                    Width = 100,
                    Height = 100,
                    Format = "png",
                    FileSizeBytes = 1000,
                },
            ]);

        var response = await _client.GetAsync("/api/v1/product-data?type=image", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<ProductDataContractResponse[]>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        items.ShouldNotBeNull();
        items!.Length.ShouldBe(1);
        items[0].Type.ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var image = new ImageProductData
        {
            TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Title = "Banner",
            Width = 800,
            Height = 600,
            Format = "jpg",
            FileSizeBytes = 200000,
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(image.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image);

        var response = await _client.GetAsync($"/api/v1/product-data/{image.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var data = await response.Content.ReadFromJsonAsync<ProductDataContractResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        data.ShouldNotBeNull();
        data!.Title.ShouldBe("Banner");
        data.Type.ShouldBe("image");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductData?)null);

        var response = await _client.GetAsync($"/api/v1/product-data/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    public async Task Create_ValidRequest_ReturnsCreated(string type)
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        if (type == "image")
        {
            _repositoryMock
                .Setup(r =>
                    r.CreateAsync(It.IsAny<ImageProductData>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((ProductData d, CancellationToken _) => d);
        }
        else
        {
            _repositoryMock
                .Setup(r =>
                    r.CreateAsync(It.IsAny<VideoProductData>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((ProductData d, CancellationToken _) => d);
        }

        object payload =
            type == "image"
                ? new
                {
                    Title = "Hero Banner",
                    Description = "Main page hero",
                    Width = 1920,
                    Height = 1080,
                    Format = "jpg",
                    FileSizeBytes = 500000,
                }
                : new
                {
                    Title = "Product Demo",
                    DurationSeconds = 120,
                    Resolution = "1080p",
                    Format = "mp4",
                    FileSizeBytes = 10000000,
                };

        var response = await _client.PostAsJsonAsync($"/api/v1/product-data/{type}", payload, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        var data = await response.Content.ReadFromJsonAsync<ProductDataContractResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        data.ShouldNotBeNull();
        data!.Type.ShouldBe(type);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    public async Task Create_InvalidRequest_ReturnsBadRequest(string type)
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        object payload =
            type == "image"
                ? new
                {
                    Title = "",
                    Width = -1,
                    Height = 0,
                    Format = "bmp",
                    FileSizeBytes = -100,
                }
                : new
                {
                    Title = "",
                    DurationSeconds = 0,
                    Resolution = "480p",
                    Format = "wmv",
                    FileSizeBytes = -1,
                };

        var response = await _client.PostAsJsonAsync($"/api/v1/product-data/{type}", payload, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_WithToken_ReturnsNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ImageProductData
                {
                    Id = id,
                    TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Title = "Image",
                }
            );

        var response = await _client.DeleteAsync($"/api/v1/product-data/{id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _repositoryMock.Verify(
            r =>
                r.SoftDeleteAsync(
                    id,
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
