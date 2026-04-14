using Polly;

namespace ProductCatalog.Interfaces;

/// <summary>
///     Provides the configured MongoDB product-data soft-delete resilience pipeline.
/// </summary>
public interface IMongoProductDataDeletePipelineProvider
{
    ResiliencePipeline Get();
}
