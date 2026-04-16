using ErrorOr;
using ProductCatalog.ValueObjects;
using SharedKernel.Application.Validation;
using SystemTextJsonPatch;
using Wolverine;
using IProductRepository = ProductCatalog.Interfaces.IProductRepository;

namespace ProductCatalog.Features.Product.PatchProduct;

public sealed record PatchProductCommand(
    Guid Id,
    JsonPatchDocument<PatchableProductDto> PatchDocument
) : IHasId;

public sealed class PatchProductCommandHandler
{
    public static async Task<(ErrorOr<ProductResponse>, OutgoingMessages)> HandleAsync(
        PatchProductCommand command,
        IProductRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        IValidator validator,
        CancellationToken ct
    )
    {
        Entities.Product? product = await repository.GetByIdAsync(command.Id, ct);
        if (product is null)
            return (DomainErrors.Products.NotFound(command.Id), OutgoingMessagesHelper.Empty);

        PatchableProductDto dto = new()
        {
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            CategoryId = product.CategoryId,
        };

        try
        {
            command.PatchDocument.ApplyTo(dto);
        }
        catch (Exception ex)
        {
            return (
                DomainErrors.Patch.InvalidPatchDocument(ex.Message),
                OutgoingMessagesHelper.Empty
            );
        }

        IReadOnlyList<System.ComponentModel.DataAnnotations.ValidationResult> validationResults =
            validator.Validate(dto);
        if (validationResults.Count > 0)
        {
            return (
                DomainErrors.Patch.InvalidPatchDocument(
                    string.Join(
                        "; ",
                        validationResults
                            .Select(e => e.ErrorMessage)
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                    )
                ),
                OutgoingMessagesHelper.Empty
            );
        }

        ErrorOr<Price> priceResult = Price.Create(dto.Price);
        if (priceResult.IsError)
            return (priceResult.FirstError, OutgoingMessagesHelper.Empty);

        product.UpdateDetails(dto.Name, dto.Description, priceResult.Value, dto.CategoryId);

        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.UpdateAsync(product, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));

        return (product.ToResponse(), messages);
    }
}
