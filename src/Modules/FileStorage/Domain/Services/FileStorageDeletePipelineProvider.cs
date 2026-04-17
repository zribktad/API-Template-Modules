using Polly.Registry;
using SharedKernel.Application.Resilience;
using SharedKernel.Infrastructure.Resilience;

namespace FileStorage.Domain.Services;

internal sealed class FileStorageDeletePipelineProvider(ResiliencePipelineProvider<string> provider)
    : ResiliencePipelineProviderBase(provider, ResiliencePipelineKeys.FileStorageDelete),
        IFileStorageDeletePipelineProvider;
