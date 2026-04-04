namespace SharedKernel.Contracts.Commands.Cleanup;

/// <summary>
///     Cross-module command instructing the ProductCatalog module to delete orphaned product data documents.
///     Dispatched by the BackgroundJobs cleanup orchestrator via the message bus.
/// </summary>
public sealed record CleanupOrphanedProductDataCommand(int RetentionDays, int BatchSize);
