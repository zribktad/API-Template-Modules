using Polly.Registry;
using ProductCatalog.Interfaces;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Resilience;

namespace ProductCatalog.Services;

public sealed class MongoProductDataDeletePipelineProvider(ResiliencePipelineProvider<string> provider)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.MongoProductDataDelete),
      IMongoProductDataDeletePipelineProvider;
