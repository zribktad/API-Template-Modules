using Contracts.Events;
using ErrorOr;
using FluentValidation;
using ProductCatalog.Domain;
using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Wolverine;
using CategoryEntity = ProductCatalog.Domain.Entities.Category;

namespace ProductCatalog.Application.Features.Category;

/// <summary>Creates multiple categories in a single batch operation.</summary>
public sealed record CreateCategoriesCommand(CreateCategoriesRequest Request);

/// <summary>Handles <see cref="CreateCategoriesCommand"/> by validating all items and persisting in a single transaction.</summary>
public sealed class CreateCategoriesCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<CategoryEntity>?,
        OutgoingMessages
    )> LoadAsync(
        CreateCategoriesCommand command,
        IValidator<CreateCategoryRequest> itemValidator,
        CancellationToken ct
    )
    {
        IReadOnlyList<CreateCategoryRequest> items = command.Request.Items;
        BatchFailureContext<CreateCategoryRequest> context = new(items);

        await context.ApplyRulesAsync(
            ct,
            new FluentValidationBatchRule<CreateCategoryRequest>(itemValidator)
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        List<CategoryEntity> entities = items
            .Select(item => new CategoryEntity
            {
                Id = Guid.NewGuid(),
                Name = item.Name,
                Description = item.Description,
            })
            .ToList();

        return (HandlerContinuation.Continue, entities, new OutgoingMessages());
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        CreateCategoriesCommand command,
        IReadOnlyList<CategoryEntity> entities,
        ICategoryRepository repository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.AddRangeAsync(entities, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        return (new BatchResponse([], command.Request.Items.Count, 0), messages);
    }
}
