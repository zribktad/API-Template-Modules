using APITemplate.Api.Features.Examples.DTOs;
using Contracts.Events;
using ErrorOr;
using FluentValidation;
using ProductCatalog.Application.Features.Product.DTOs;
using ProductCatalog.Application.Features.Product.Mappings;
using ProductCatalog.Domain.Interfaces;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Domain.Interfaces;
using Wolverine;
using ProductCatalogErrors = ProductCatalog.Application.Errors.DomainErrors;

namespace APITemplate.Api.Features.Examples.Commands;

public sealed record PatchProductCommand(Guid Id, Action<PatchableProductDto> ApplyPatch) : IHasId;

public sealed class PatchProductCommandHandler
{
    public static async Task<ErrorOr<ProductResponse>> HandleAsync(
        PatchProductCommand command,
        IProductRepository repository,
        IUnitOfWork unitOfWork,
        IValidator<PatchableProductDto> validator,
        IMessageBus bus,
        CancellationToken ct
    )
    {
        ProductCatalog.Domain.Entities.Product? product = await repository.GetByIdAsync(
            command.Id,
            ct
        );
        if (product is null)
            return ProductCatalogErrors.Products.NotFound(command.Id);

        PatchableProductDto dto = new()
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            CategoryId = product.CategoryId,
        };

        command.ApplyPatch(dto);

        FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(
            dto,
            ct
        );
        if (!validationResult.IsValid)
            return ExampleErrors.InvalidPatchDocument(
                string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))
            );

        product.UpdateDetails(dto.Name, dto.Description, dto.Price, dto.CategoryId);

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.UpdateAsync(product, ct);
            },
            ct
        );

        await bus.PublishAsync(
            new CacheInvalidationNotification(ProductCatalog.Application.Events.CacheTags.Products)
        );

        return product.ToResponse();
    }
}
