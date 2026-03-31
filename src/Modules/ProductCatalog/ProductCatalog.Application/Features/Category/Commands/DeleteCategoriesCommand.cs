using SharedKernel.Application.Batch;
using SharedKernel.Application.Batch.Rules;
using Contracts.Events;
using ProductCatalog.Application.Features.Category.Specifications;
using ErrorOr;
using Wolverine;

namespace ProductCatalog.Application.Features.Category;

/// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
public sealed record DeleteCategoriesCommand(BatchDeleteRequest Request);

/// <summary>Handles <see cref="DeleteCategoriesCommand"/> by loading all categories and deleting in a single transaction.</summary>
public sealed class DeleteCategoriesCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        IReadOnlyList<ProductCatalog.Domain.Entities.Category>?,
        OutgoingMessages
    )> LoadAsync(
        DeleteCategoriesCommand command,
        ICategoryRepository repository,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        BatchFailureContext<Guid> context = new(ids);

        // Load all target categories and mark missing ones as failed
        IReadOnlyList<ProductCatalog.Domain.Entities.Category> categories = await repository.ListAsync(
            new CategoriesByIdsSpecification(ids.ToHashSet()),
            ct
        );

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<Guid>(
                id => id,
                categories.Select(category => category.Id).ToHashSet(),
                ErrorCatalog.Categories.NotFoundMessage
            )
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (HandlerContinuation.Continue, categories, new OutgoingMessages());
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        DeleteCategoriesCommand command,
        IReadOnlyList<ProductCatalog.Domain.Entities.Category> categories,
        ICategoryRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken ct
    )
    {
        // Remove categories in a single transaction
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await repository.DeleteRangeAsync(categories, ct);
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));

        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }
}


