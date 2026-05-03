using BuildingBlocks.Application.Resilience;
using BuildingBlocks.Web.Resilience;
using Polly.Registry;

namespace FileStorage.Domain.Services;

internal sealed class FileStorageDeletePipelineProvider(ResiliencePipelineProvider<string> provider)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.FileStorageDelete),
        IFileStorageDeletePipelineProvider;
