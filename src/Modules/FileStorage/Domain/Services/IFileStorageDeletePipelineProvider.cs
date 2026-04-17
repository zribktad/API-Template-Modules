using Polly;

namespace FileStorage.Domain.Services;

/// <summary>
///     Provides the configured file-storage delete (rollback / cleanup) resilience pipeline.
/// </summary>
public interface IFileStorageDeletePipelineProvider
{
    ResiliencePipeline Get();
}
