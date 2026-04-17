using ErrorOr;
using Wolverine;

namespace ProductCatalog.Features.Category.DeleteCategories;

/// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
public sealed record DeleteCategoriesCommand(BatchDeleteRequest Request);

/// <summary>
///     Handles <see cref="DeleteCategoriesCommand" /> using a two-stage pattern.
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>LoadAsync</c> validates that all requested IDs exist and captures actor/time context.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>HandleAsync</c> uses two <c>ExecuteUpdateAsync</c> statements inside one transaction:
///                 first clears <c>CategoryId</c> on affected products, then bulk-soft-deletes the categories.
///                 The order is required — products must be un-linked before categories become invisible
///                 to queries via the soft-delete global filter.
///             </description>
///         </item>
///     </list>
/// </summary>
public sealed class DeleteCategoriesCommandHandler
{
    public static async Task<(
        HandlerContinuation,
        DeleteCategoriesState?,
        OutgoingMessages
    )> LoadAsync(
        DeleteCategoriesCommand command,
        ICategoryRepository repository,
        IActorProvider actorProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        BatchFailureContext<Guid> context = new(ids);

        IReadOnlyList<Entities.Category> categories = await repository.ListAsync(
            new CategoriesByIdsSpecification(ids.ToHashSet()),
            ct
        );

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<Guid>(
                id => id,
                categories.Select(c => c.Id).ToHashSet(),
                ErrorCatalog.Categories.NotFoundMessage
            )
        );

        if (context.HasFailures)
        {
            OutgoingMessages failureMessages = new();
            failureMessages.RespondToSender(context.ToFailureResponse());
            return (HandlerContinuation.Stop, null, failureMessages);
        }

        return (
            HandlerContinuation.Continue,
            new DeleteCategoriesState(
                categories.Select(c => c.Id).ToList(),
                actorProvider.ActorId,
                timeProvider.GetUtcNow().UtcDateTime
            ),
            OutgoingMessagesHelper.Empty
        );
    }

    public static async Task<(ErrorOr<BatchResponse>, OutgoingMessages)> HandleAsync(
        DeleteCategoriesCommand command,
        DeleteCategoriesState state,
        ICategoryRepository repository,
        IProductRepository productRepository,
        IUnitOfWork<ProductCatalogDbMarker> unitOfWork,
        CancellationToken ct
    )
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                // Products must be un-linked before categories are soft-deleted; the global query
                // filter would otherwise hide products whose CategoryId matches a deleted category.
                await productRepository.ClearCategoryAsync(state.CategoryIds, ct);
                await repository.BulkSoftDeleteByIdsAsync(
                    state.CategoryIds,
                    state.ActorId,
                    state.DeletedAtUtc,
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.Add(new CacheInvalidationNotification(CacheTags.Categories));
        messages.Add(new CacheInvalidationNotification(CacheTags.Products));

        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }

    public sealed record DeleteCategoriesState(
        IReadOnlyList<Guid> CategoryIds,
        Guid ActorId,
        DateTime DeletedAtUtc
    );
}
