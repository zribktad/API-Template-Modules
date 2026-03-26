using APITemplate.Application.Common.Batch;
using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Product;
using APITemplate.Application.Features.Product.DTOs;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Entities.ProductData;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Options;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class ProductRequestHandlersTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMessageBus> _busMock;
    private readonly Mock<IValidator<CreateProductRequest>> _createValidatorMock;
    private readonly Mock<IValidator<UpdateProductItem>> _updateValidatorMock;

    public ProductRequestHandlersTests()
    {
        _repositoryMock = new Mock<IProductRepository>();
        _categoryRepositoryMock = new Mock<ICategoryRepository>();
        _productDataRepositoryMock = new Mock<IProductDataRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _busMock = new Mock<IMessageBus>();
        _createValidatorMock = new Mock<IValidator<CreateProductRequest>>();
        _updateValidatorMock = new Mock<IValidator<UpdateProductItem>>();
        _unitOfWorkMock.SetupImmediateTransactionExecution();
        _unitOfWorkMock.SetupImmediateTransactionExecution<Product>();

        // Default: validation passes
        _createValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<CreateProductRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ValidationResult());
        _updateValidatorMock
            .Setup(v =>
                v.ValidateAsync(It.IsAny<UpdateProductItem>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ValidationResult());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByIdAsync_ReturnsExpectedResult(bool productExists)
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = Guid.NewGuid();
        ProductResponse? response = productExists
            ? new ProductResponse(
                productId,
                "Test Product",
                "A test product",
                9.99m,
                Guid.NewGuid(),
                DateTime.UtcNow,
                []
            )
            : null;

        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<ProductByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        var result = await GetProductByIdQueryHandler.HandleAsync(
            new GetProductByIdQuery(productId),
            _repositoryMock.Object,
            ct
        );

        if (productExists)
        {
            result.IsError.ShouldBeFalse();
            result.Value.Name.ShouldBe("Test Product");
            result.Value.Price.ShouldBe(9.99m);
        }
        else
        {
            result.IsError.ShouldBeTrue();
            result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        }
    }

    [Fact]
    public async Task BatchCreateAsync_ReturnsCreatedProduct()
    {
        var request = new CreateProductRequest("New Product", "Description", 19.99m);
        var batchRequest = new CreateProductsRequest([request]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r =>
                r.AddRangeAsync(
                    It.Is<IEnumerable<Product>>(ps => ps.Count() == 1),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchCreateAsync_WithProductDataIds_NormalizesAndStoresUniqueLinks()
    {
        var productDataId = Guid.NewGuid();
        var request = new CreateProductRequest(
            "New Product",
            "Description",
            19.99m,
            null,
            [productDataId, productDataId]
        );
        var batchRequest = new CreateProductsRequest([request]);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r =>
                r.AddRangeAsync(
                    It.Is<IEnumerable<Product>>(ps =>
                        ps.Single().ProductDataLinks.Count == 1
                        && ps.Single().ProductDataLinks.Single().ProductDataId == productDataId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchCreateAsync_WithCategoryId_ValidatesCategory()
    {
        var categoryId = Guid.NewGuid();
        var category = new Category { Id = categoryId, Name = "Test" };
        var request = new CreateProductRequest("New Product", "Description", 19.99m, categoryId);
        var batchRequest = new CreateProductsRequest([request]);

        _categoryRepositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<Application.Features.Category.Specifications.CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([category]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchCreateAsync_WithNonExistentCategory_ReturnsFailure()
    {
        var categoryId = Guid.NewGuid();
        var request = new CreateProductRequest("New Product", "Description", 19.99m, categoryId);
        var batchRequest = new CreateProductsRequest([request]);

        _categoryRepositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<Application.Features.Category.Specifications.CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain(e => e.Contains("Category"));
    }

    [Fact]
    public async Task BatchCreateAsync_WithMissingProductData_ReturnsFailure()
    {
        var productDataId = Guid.NewGuid();
        var request = new CreateProductRequest(
            "New Product",
            "Description",
            19.99m,
            null,
            [productDataId]
        );
        var batchRequest = new CreateProductsRequest([request]);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures[0].Errors.ShouldContain(e => e.Contains("Product data not found"));
    }

    [Fact]
    public async Task BatchCreateAsync_GeneratesIdsServerSide()
    {
        var request = new CreateProductsRequest([
            new CreateProductRequest("First", null, 10m),
            new CreateProductRequest("Second", null, 20m),
        ]);

        IEnumerable<Product>? captured = null;
        _repositoryMock
            .Setup(r =>
                r.AddRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>())
            )
            .Callback<IEnumerable<Product>, CancellationToken>(
                (entities, _) => captured = entities.ToList()
            )
            .ReturnsAsync(
                (IEnumerable<Product> entities, CancellationToken _) => entities.ToList()
            );

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(request),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(2);
        result.Value.FailureCount.ShouldBe(0);
        captured.ShouldNotBeNull();
        captured!.All(x => x.Id != Guid.Empty).ShouldBeTrue();
        captured.Select(x => x.Id).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task BatchUpdateAsync_WhenProductNotFound_ReturnsFailure()
    {
        var productId = Guid.NewGuid();
        var item = new UpdateProductItem(productId, "Name", null, 10m);
        var batchRequest = new UpdateProductsRequest([item]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        (BatchResponse? failure, _) = await UpdateProductsValidator.ValidateAndLoadAsync(
            new UpdateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        failure.ShouldNotBeNull();
        failure.FailureCount.ShouldBe(1);
        failure.Failures[0].Errors.ShouldContain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task LoadAsync_WhenProductNotFound_ReturnsStop()
    {
        var productId = Guid.NewGuid();
        var item = new UpdateProductItem(productId, "Name", null, 10m);
        var batchRequest = new UpdateProductsRequest([item]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        (
            HandlerContinuation continuation,
            EntityLookup<Product>? lookup,
            OutgoingMessages messages
        ) = await UpdateProductsCommandHandler.LoadAsync(
            new UpdateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Stop);
        lookup.ShouldBeNull();
        messages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_WhenValid_ReturnsContinueWithLookup()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            ProductDataLinks = [],
        };
        var item = new UpdateProductItem(product.Id, "Updated", null, 20m);
        var batchRequest = new UpdateProductsRequest([item]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([product]);

        (
            HandlerContinuation continuation,
            EntityLookup<Product>? lookup,
            OutgoingMessages messages
        ) = await UpdateProductsCommandHandler.LoadAsync(
            new UpdateProductsCommand(batchRequest),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        continuation.ShouldBe(HandlerContinuation.Continue);
        lookup.ShouldNotBeNull();
        lookup.Entities.ShouldContainKey(product.Id);
        messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchDeleteAsync_CallsRepositoryDelete()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Delete me",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = Guid.NewGuid() },
            ],
        };

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([product]);

        var batchRequest = new BatchDeleteRequest([product.Id]);

        var result = await DeleteProductsCommandHandler.HandleAsync(
            new DeleteProductsCommand(batchRequest),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.FailureCount.ShouldBe(0);
        result.Value.Failures.ShouldBeEmpty();

        _repositoryMock.Verify(
            r =>
                r.DeleteRangeAsync(
                    It.Is<IEnumerable<Product>>(p => p.Contains(product)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
        product.ProductDataLinks.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchUpdateAsync_WhenProductExists_UpdatesFields()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Desc",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            ProductDataLinks = [],
        };

        var item = new UpdateProductItem(product.Id, "New Name", "New Desc", 20m);
        var batchRequest = new UpdateProductsRequest([item]);
        EntityLookup<Product> lookup = new(
            new Dictionary<Guid, Product> { [product.Id] = product }
        );

        var result = await UpdateProductsCommandHandler.HandleAsync(
            new UpdateProductsCommand(batchRequest),
            lookup,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        result.Value.Failures.ShouldBeEmpty();

        product.Name.ShouldBe("New Name");
        product.Description.ShouldBe("New Desc");
        product.Price.ShouldBe(20m);

        _repositoryMock.Verify(
            r => r.UpdateAsync(product, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task BatchUpdateAsync_ReplacesProductDataLinks()
    {
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = oldId },
            ],
        };
        var item = new UpdateProductItem(product.Id, "New Name", "New Desc", 20m, null, [newId]);
        var batchRequest = new UpdateProductsRequest([item]);
        EntityLookup<Product> lookup = new(
            new Dictionary<Guid, Product> { [product.Id] = product }
        );

        var result = await UpdateProductsCommandHandler.HandleAsync(
            new UpdateProductsCommand(batchRequest),
            lookup,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        product.ProductDataLinks.Select(x => x.ProductDataId).ShouldBe([newId]);
    }

    [Fact]
    public async Task BatchUpdateAsync_WithEmptyProductDataIds_RemovesExistingLinks()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = Guid.NewGuid() },
            ],
        };
        var item = new UpdateProductItem(product.Id, "New Name", "New Desc", 20m, null, []);
        var batchRequest = new UpdateProductsRequest([item]);
        EntityLookup<Product> lookup = new(
            new Dictionary<Guid, Product> { [product.Id] = product }
        );

        var result = await UpdateProductsCommandHandler.HandleAsync(
            new UpdateProductsCommand(batchRequest),
            lookup,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        product.ProductDataLinks.ShouldBeEmpty();
    }

    [Fact]
    public async Task BatchUpdateAsync_WithNullProductDataIds_KeepsExistingLinks()
    {
        var existingId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks =
            [
                new ProductDataLink { ProductId = Guid.NewGuid(), ProductDataId = existingId },
            ],
        };
        var item = new UpdateProductItem(product.Id, "New Name", "New Desc", 20m, null, null);
        var batchRequest = new UpdateProductsRequest([item]);
        EntityLookup<Product> lookup = new(
            new Dictionary<Guid, Product> { [product.Id] = product }
        );

        var result = await UpdateProductsCommandHandler.HandleAsync(
            new UpdateProductsCommand(batchRequest),
            lookup,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        product.ProductDataLinks.Select(x => x.ProductDataId).ShouldBe([existingId]);
    }

    [Fact]
    public async Task BatchUpdateAsync_RestoresSoftDeletedProductDataLink()
    {
        var restoredId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Old Name",
            Price = 10m,
            ProductDataLinks = [],
        };
        var deletedLink = ProductDataLink.Create(product.Id, restoredId);
        deletedLink.IsDeleted = true;
        deletedLink.DeletedAtUtc = DateTime.UtcNow;
        deletedLink.DeletedBy = Guid.NewGuid();

        var item = new UpdateProductItem(product.Id, "New Name", null, 20m, null, [restoredId]);
        var batchRequest = new UpdateProductsRequest([item]);
        EntityLookup<Product> lookup = new(
            new Dictionary<Guid, Product> { [product.Id] = product }
        );

        var result = await UpdateProductsCommandHandler.HandleAsync(
            new UpdateProductsCommand(batchRequest),
            lookup,
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.SuccessCount.ShouldBe(1);
        product.ProductDataLinks.Select(x => x.ProductDataId).ShouldBe([restoredId]);
        product.ProductDataLinks.Single().IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public async Task BatchCreateAsync_MultipleFailureTypes_ReportsFirstErrorPerItem()
    {
        // Item 0: validation failure (bad name via validator)
        // Item 1: missing category
        // Item 2: valid
        var missingCategoryId = Guid.NewGuid();
        var request = new CreateProductsRequest([
            new CreateProductRequest("", "Desc", 10m),
            new CreateProductRequest("Good Name", "Desc", 10m, missingCategoryId),
            new CreateProductRequest("Also Good", "Desc", 20m),
        ]);

        _createValidatorMock
            .Setup(v =>
                v.ValidateAsync(
                    It.Is<CreateProductRequest>(r => r.Name == ""),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ValidationResult([new ValidationFailure("Name", "Name is required.")])
            );

        _categoryRepositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<Application.Features.Category.Specifications.CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(request),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.FailureCount.ShouldBe(2);
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.Failures.Count.ShouldBe(2);
        result.Value.Failures.ShouldContain(f => f.Index == 0);
        result.Value.Failures.ShouldContain(f => f.Index == 1);

        _repositoryMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task BatchCreateAsync_MissingCategoryAndProductData_MergesErrorsForSameItem()
    {
        var missingCategoryId = Guid.NewGuid();
        var missingPdId = Guid.NewGuid();
        var request = new CreateProductsRequest([
            new CreateProductRequest("Good Name", "Desc", 10m, missingCategoryId, [missingPdId]),
        ]);

        _categoryRepositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<Application.Features.Category.Specifications.CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        var result = await CreateProductsCommandHandler.HandleAsync(
            new CreateProductsCommand(request),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            _createValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.FailureCount.ShouldBe(1);
        result.Value.Failures.Count.ShouldBe(1);
        result.Value.Failures[0].Index.ShouldBe(0);
        result.Value.Failures[0].Errors.Count.ShouldBe(2);
        result
            .Value.Failures[0]
            .Errors.ShouldContain(e => e.Contains("Category", StringComparison.Ordinal));
        result.Value.Failures[0].Errors.ShouldContain(e => e.Contains("Product data not found"));

        _repositoryMock.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task BatchUpdateAsync_ValidationAndMissing_ReportsFirstErrorPerItem()
    {
        // Item 0: validation failure
        // Item 1: not found (missing entity)
        // Item 2: valid
        var existingProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            ProductDataLinks = [],
        };

        var items = new UpdateProductsRequest([
            new UpdateProductItem(Guid.NewGuid(), "", null, 10m),
            new UpdateProductItem(Guid.NewGuid(), "Good Name", null, 20m),
            new UpdateProductItem(existingProduct.Id, "Updated", null, 30m),
        ]);

        _updateValidatorMock
            .Setup(v =>
                v.ValidateAsync(
                    It.Is<UpdateProductItem>(i => i.Name == ""),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ValidationResult([new ValidationFailure("Name", "Name is required.")])
            );

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([existingProduct]);

        (BatchResponse? failure, _) = await UpdateProductsValidator.ValidateAndLoadAsync(
            new UpdateProductsCommand(items),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        failure.ShouldNotBeNull();
        failure.FailureCount.ShouldBe(2);
        failure.SuccessCount.ShouldBe(0);
        failure.Failures.Count.ShouldBe(2);
        failure.Failures.ShouldContain(f => f.Index == 0 && f.Errors.Any(e => e.Contains("Name")));
        failure.Failures.ShouldContain(f =>
            f.Index == 1 && f.Errors.Any(e => e.Contains("not found"))
        );
    }

    [Fact]
    public async Task BatchUpdateAsync_MissingCategoryAndProductData_MergesErrorsForSameItem()
    {
        var missingCategoryId = Guid.NewGuid();
        var missingPdId = Guid.NewGuid();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Existing",
            Price = 10m,
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
            ProductDataLinks = [],
        };

        var items = new UpdateProductsRequest([
            new UpdateProductItem(
                product.Id,
                "Updated",
                null,
                20m,
                missingCategoryId,
                [missingPdId]
            ),
        ]);

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([product]);

        _categoryRepositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<Application.Features.Category.Specifications.CategoriesByIdsSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        _productDataRepositoryMock
            .Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([]);

        (BatchResponse? failure, _) = await UpdateProductsValidator.ValidateAndLoadAsync(
            new UpdateProductsCommand(items),
            _repositoryMock.Object,
            _categoryRepositoryMock.Object,
            _productDataRepositoryMock.Object,
            _updateValidatorMock.Object,
            TestContext.Current.CancellationToken
        );

        failure.ShouldNotBeNull();
        failure.FailureCount.ShouldBe(1);
        failure.Failures.Count.ShouldBe(1);
        failure.Failures[0].Index.ShouldBe(0);
        failure.Failures[0].Id.ShouldBe(product.Id);
        failure.Failures[0].Errors.Count.ShouldBe(2);
        failure
            .Failures[0]
            .Errors.ShouldContain(e => e.Contains("Category", StringComparison.Ordinal));
        failure.Failures[0].Errors.ShouldContain(e => e.Contains("Product data not found"));
    }

    [Fact]
    public async Task BatchDeleteAsync_SomeMissing_ReportsOnlyMissing()
    {
        var existingProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Exists",
            Price = 10m,
            ProductDataLinks = [],
        };
        var missingId = Guid.NewGuid();

        _repositoryMock
            .Setup(r =>
                r.ListAsync(
                    It.IsAny<ProductsByIdsWithLinksSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([existingProduct]);

        var result = await DeleteProductsCommandHandler.HandleAsync(
            new DeleteProductsCommand(new BatchDeleteRequest([existingProduct.Id, missingId])),
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _busMock.Object,
            TestContext.Current.CancellationToken
        );

        result.IsError.ShouldBeFalse();
        result.Value.FailureCount.ShouldBe(1);
        result.Value.SuccessCount.ShouldBe(0);
        result.Value.Failures.ShouldHaveSingleItem();
        result.Value.Failures[0].Index.ShouldBe(1);
        result.Value.Failures[0].Id.ShouldBe(missingId);

        _repositoryMock.Verify(
            r =>
                r.DeleteRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts()
    {
        var ct = TestContext.Current.CancellationToken;
        var filter = new ProductFilter();
        IReadOnlyList<ProductResponse> items =
        [
            new ProductResponse(
                Guid.NewGuid(),
                "Product 1",
                null,
                10m,
                Guid.NewGuid(),
                DateTime.UtcNow,
                []
            ),
            new ProductResponse(
                Guid.NewGuid(),
                "Product 2",
                null,
                20m,
                Guid.NewGuid(),
                DateTime.UtcNow,
                []
            ),
        ];

        var paged = new PagedResponse<ProductResponse>(
            items,
            2,
            filter.PageNumber,
            filter.PageSize
        );
        _repositoryMock
            .Setup(r => r.GetPagedAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);
        _repositoryMock
            .Setup(r => r.GetCategoryFacetsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repositoryMock
            .Setup(r => r.GetPriceFacetsAsync(filter, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await GetProductsQueryHandler.HandleAsync(
            new GetProductsQuery(filter),
            _repositoryMock.Object,
            ct
        );

        result.IsError.ShouldBeFalse();
        result.Value.Page.Items.Count().ShouldBe(2);
        result.Value.Page.TotalCount.ShouldBe(2);
    }
}
