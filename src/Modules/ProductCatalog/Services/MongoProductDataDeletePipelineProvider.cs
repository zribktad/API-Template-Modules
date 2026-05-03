using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Web.Resilience;
using Polly.Registry;
using ProductCatalog.Interfaces;

namespace ProductCatalog.Services;

public sealed class MongoProductDataDeletePipelineProvider(
    ResiliencePipelineProvider<string> provider
)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.MongoProductDataDelete),
        IMongoProductDataDeletePipelineProvider;
