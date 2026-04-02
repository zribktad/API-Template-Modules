using Contracts.Events;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using ProductCatalog.Application.Events;
using ProductCatalog.Application.Features.Product.Commands;
using ProductCatalog.Application.Features.Product.DTOs;
using ProductCatalog.Domain;
using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Interfaces;
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
            Price = 10m,
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
        product.Price.ShouldBe(25m);

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
}
