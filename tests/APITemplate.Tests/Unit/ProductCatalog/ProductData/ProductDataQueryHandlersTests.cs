using APITemplate.Tests.Unit.Infrastructure;
using BuildingBlocks.Application.Context;
using ErrorOr;
using global::ProductCatalog.Features.ProductData.GetProductData;
using global::ProductCatalog.Features.ProductData.GetProductDataById;
using global::ProductCatalog.Features.ProductData.Shared;
using global::ProductCatalog.Interfaces;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class ProductDataQueryHandlersTests
{
    private readonly Mock<IProductDataRepository> _repository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    [Fact]
    public async Task GetProductData_ShouldMapRepositoryItemsToResponses()
    {
        _repository
            .Setup(r => r.GetAllAsync("image", It.IsAny<CancellationToken>()))
            .ReturnsAsync([DomainTestDataFactory.ImageProductData()]);

        ErrorOr<List<ProductDataResponse>> result = await GetProductDataQueryHandler.HandleAsync(
            new GetProductDataQuery("image"),
            _repository.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Type.ShouldBe("image");
    }

    [Fact]
    public async Task GetProductDataById_WhenMissingOrTenantMismatch_ShouldReturnNotFound()
    {
        Guid id = Guid.NewGuid();
        _tenantProvider.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainTestDataFactory.ImageProductData(id: id, tenantId: Guid.NewGuid()));

        ErrorOr<ProductDataResponse> result = await GetProductDataByIdQueryHandler.HandleAsync(
            new GetProductDataByIdQuery(id),
            _repository.Object,
            _tenantProvider.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task GetProductDataById_WhenFoundForTenant_ShouldReturnMappedResponse()
    {
        Guid id = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        _tenantProvider.SetupGet(x => x.TenantId).Returns(tenantId);
        _repository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainTestDataFactory.VideoProductData(id: id, tenantId: tenantId));

        ErrorOr<ProductDataResponse> result = await GetProductDataByIdQueryHandler.HandleAsync(
            new GetProductDataByIdQuery(id),
            _repository.Object,
            _tenantProvider.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(id);
    }
}
