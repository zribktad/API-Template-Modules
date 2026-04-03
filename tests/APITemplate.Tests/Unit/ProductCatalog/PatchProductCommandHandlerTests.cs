using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using Identity.Domain.ValueObjects;
using Moq;
using ProductCatalog.Application.Events;
using ProductCatalog.Application.Features.Product.Commands;
using ProductCatalog.Application.Features.Product.DTOs;
using ProductCatalog.Application.Features.Product.Repositories;
using ProductCatalog.Domain;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.ValueObjects;
using SharedKernel.Contracts.Events;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Domain.Options;
using Shouldly;
using SystemTextJsonPatch;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

public sealed class PatchProductCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenPatchIsValid_ReturnsUpdatedProductAndCacheInvalidation()
    {
        Mock<IProductRepository> repositoryMock = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWorkMock = new();
        Mock<IValidator<PatchableProductDto>> validatorMock = new();

        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old Description",
            Price = Price.FromPersistence(10m),
            CategoryId = Guid.NewGuid(),
        };

        JsonPatchDocument<PatchableProductDto> patchDocument = new();
        patchDocument.Replace(p => p.Name, "New Name");
        patchDocument.Replace(p => p.Price, 25m);

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        repositoryMock
            .Setup(repository => repository.UpdateAsync(product, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        unitOfWorkMock
            .Setup(unitOfWork =>
                unitOfWork.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                )
            )
            .Returns<Func<Task>, CancellationToken, TransactionOptions?>(
                async (action, _, _) => await action()
            );

        validatorMock
            .Setup(validator =>
                validator.ValidateAsync(
                    It.IsAny<PatchableProductDto>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ValidationResult());

        (ErrorOr<ProductResponse> result, OutgoingMessages messages) =
            await PatchProductCommandHandler.HandleAsync(
                new PatchProductCommand(product.Id, patchDocument),
                repositoryMock.Object,
                unitOfWorkMock.Object,
                validatorMock.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("New Name");
        result.Value.Price.ShouldBe(25m);
        product.Name.ShouldBe("New Name");
        product.Price.Value.ShouldBe(25m);

        messages.Count.ShouldBe(1);
        messages[0].ShouldBeOfType<CacheInvalidationNotification>();
        ((CacheInvalidationNotification)messages[0]).CacheTag.ShouldBe(CacheTags.Products);

        repositoryMock.Verify(
            repository => repository.UpdateAsync(product, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAsync_WhenProductDoesNotExist_ReturnsErrorWithoutMessages()
    {
        Mock<IProductRepository> repositoryMock = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWorkMock = new();
        Mock<IValidator<PatchableProductDto>> validatorMock = new();
        Guid productId = Guid.NewGuid();

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        (ErrorOr<ProductResponse> result, OutgoingMessages messages) =
            await PatchProductCommandHandler.HandleAsync(
                new PatchProductCommand(productId, new JsonPatchDocument<PatchableProductDto>()),
                repositoryMock.Object,
                unitOfWorkMock.Object,
                validatorMock.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeTrue();
        messages.ShouldBeEmpty();

        unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task HandleAsync_WhenPatchSetsNegativePrice_ReturnsValidationError()
    {
        Mock<IProductRepository> repositoryMock = new();
        Mock<IUnitOfWork<ProductCatalogDbMarker>> unitOfWorkMock = new();
        Mock<IValidator<PatchableProductDto>> validatorMock = new();

        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = "Product",
            Description = null,
            Price = Price.FromPersistence(10m),
            CategoryId = Guid.NewGuid(),
        };

        JsonPatchDocument<PatchableProductDto> patchDocument = new();
        patchDocument.Replace(p => p.Price, -5m);

        repositoryMock
            .Setup(repository => repository.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        validatorMock
            .Setup(validator =>
                validator.ValidateAsync(
                    It.IsAny<PatchableProductDto>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ValidationResult());

        (ErrorOr<ProductResponse> result, OutgoingMessages messages) =
            await PatchProductCommandHandler.HandleAsync(
                new PatchProductCommand(product.Id, patchDocument),
                repositoryMock.Object,
                unitOfWorkMock.Object,
                validatorMock.Object,
                TestContext.Current.CancellationToken
            );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        messages.ShouldBeEmpty();

        unitOfWorkMock.Verify(
            unitOfWork =>
                unitOfWork.ExecuteInTransactionAsync(
                    It.IsAny<Func<Task>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<TransactionOptions?>()
                ),
            Times.Never
        );
    }
}

public sealed class PriceCreateTests
{
    [Fact]
    public void Create_WhenValueIsNegative_ReturnsError()
    {
        ErrorOr<Price> result = Price.Create(-1m);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_WhenValueIsZero_ReturnsValidPriceWithValueZero()
    {
        ErrorOr<Price> result = Price.Create(0m);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(0m);
    }

    [Fact]
    public void Create_WhenValueIsPositive_ReturnsValidPriceWithCorrectValue()
    {
        ErrorOr<Price> result = Price.Create(9.99m);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(9.99m);
    }
}

public sealed class EmailCreateTests
{
    [Theory]
    [InlineData("@")]
    [InlineData("abc@")]
    [InlineData("plain-text")]
    public void Create_WhenFormatIsInvalid_ReturnsValidationError(string input)
    {
        ErrorOr<Email> result = Email.Create(input);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("Invalid email format.");
    }

    [Fact]
    public void Create_WhenFormatIsValid_ReturnsNormalizedEmailValue()
    {
        ErrorOr<Email> result = Email.Create("  user@example.com  ");

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe("user@example.com");
    }
}

public sealed class ProductSyncProductDataLinksTests
{
    [Fact]
    public void SyncProductDataLinks_WhenExistingLinksContainDuplicates_DoesNotThrowAndRemovesObsoleteLinks()
    {
        Guid productId = Guid.NewGuid();
        Guid keptProductDataId = Guid.NewGuid();
        Guid removedProductDataId = Guid.NewGuid();

        Product product = new()
        {
            Id = productId,
            Name = "Product",
            Price = Price.FromPersistence(1m),
            ProductDataLinks =
            [
                ProductDataLink.Create(productId, keptProductDataId),
                ProductDataLink.Create(productId, keptProductDataId),
                ProductDataLink.Create(productId, removedProductDataId),
            ],
        };

        Should.NotThrow(() => product.SyncProductDataLinks([keptProductDataId]));

        product.ProductDataLinks.Count.ShouldBe(2);
        product.ProductDataLinks.Count(link => link.ProductDataId == keptProductDataId).ShouldBe(2);
        product
            .ProductDataLinks.Any(link => link.ProductDataId == removedProductDataId)
            .ShouldBeFalse();
    }
}
