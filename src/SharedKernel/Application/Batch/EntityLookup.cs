namespace SharedKernel.Application.Batch;

/// <summary>
/// Wraps a dictionary of loaded entities for passing between Wolverine compound-handler
/// <c>LoadAsync</c> and <c>HandleAsync</c> steps with unambiguous type matching.
/// </summary>
public sealed record EntityLookup<TEntity>(IReadOnlyDictionary<Guid, TEntity> Entities);
