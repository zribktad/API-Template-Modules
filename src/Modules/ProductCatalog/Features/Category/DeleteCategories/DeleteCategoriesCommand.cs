using ErrorOr;
using Wolverine;

namespace ProductCatalog.Features.Category.DeleteCategories;

/// <summary>Soft-deletes multiple categories in a single batch operation.</summary>
public sealed record DeleteCategoriesCommand(BatchDeleteRequest Request);

/// <summary>Transient state passed from <c>LoadAsync</c> to <c>HandleAsync</c>.</summary>
public sealed record DeleteCategoriesState(
    IReadOnlyCollection<Guid> CategoryIds,
    Guid TenantId,
    Guid ActorId,
    DateTime DeletedAtUtc
);

/// <summary>
///     Handles <see cref="DeleteCategoriesCommand" /> using a two-stage pattern.
///     <list type="bullet">
///         <item>
///             <description>
///                 <c>LoadAsync</c> validates that all requested IDs exist and captures actor/tenant/time context.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <c>HandleAsync</c> uses two <c>ExecuteUpdateAsync</c> statements inside one transaction:
///                 first clears <c>CategoryId</c> on affected products, then bulk-soft-deletes the categories.
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
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        IReadOnlyList<Guid> ids = command.Request.Ids;
        BatchFailureContext<Guid> context = new(ids);

        IReadOnlyList<Entities.Category> categories = await repository.ListAsync(
            new CategoriesByIdsSpecification(ids),
            ct
        );

        HashSet<Guid> existingIds = categories.Select(c => c.Id).ToHashSet();

        await context.ApplyRulesAsync(
            ct,
            new MarkMissingByIdBatchRule<Guid>(
                id => id,
                existingIds,
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
                existingIds,
                tenantProvider.TenantId,
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
                await productRepository.ClearCategoryAsync(state.CategoryIds, ct);
                await repository.BulkSoftDeleteByIdsAsync(
                    state.CategoryIds,
                    state.TenantId,
                    state.ActorId,
                    state.DeletedAtUtc,
                    ct
                );
            },
            ct
        );

        OutgoingMessages messages = new();
        messages.AddRange(CacheInvalidationCascades.ForCategoryDeletion());
        return (new BatchResponse([], command.Request.Ids.Count, 0), messages);
    }
}
