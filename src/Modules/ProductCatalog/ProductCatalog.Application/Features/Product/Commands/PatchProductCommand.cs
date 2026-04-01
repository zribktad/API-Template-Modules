using Contracts.Events;
using ErrorOr;
using FluentValidation;
using ProductCatalog.Application.Errors;
using ProductCatalog.Application.Features.Product.DTOs;
using ProductCatalog.Application.Features.Product.Mappings;
using IProductRepository = ProductCatalog.Domain.Interfaces.IProductRepository;
using SharedKernel.Domain.Entities.Contracts;
using SharedKernel.Domain.Interfaces;
using SystemTextJsonPatch;
using Wolverine;

namespace ProductCatalog.Application.Features.Product.Commands;

public sealed record PatchProductCommand(
    Guid Id,
    JsonPatchDocument<PatchableProductDto> PatchDocument
) : IHasId;

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
        Domain.Entities.Product? product = await repository.GetByIdAsync(
            command.Id,
            ct
        );
        if (product is null)
            return DomainErrors.Products.NotFound(command.Id);

        PatchableProductDto dto = new()
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            CategoryId = product.CategoryId,
        };

        command.PatchDocument.ApplyTo(dto);

        FluentValidation.Results.ValidationResult validationResult = await validator.ValidateAsync(
            dto,
            ct
        );
        if (!validationResult.IsValid)
            return DomainErrors.Patch.InvalidPatchDocument(
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
            new CacheInvalidationNotification(Events.CacheTags.Products)
        );

        return product.ToResponse();
    }
}
