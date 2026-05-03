using Polly;
using Polly.Registry;

namespace BuildingBlocks.Web.Resilience;

/// <summary>
///     Base class for typed resilience pipeline providers.
///     Wraps <see cref="ResiliencePipelineProvider{TKey}" /> and resolves a single named pipeline by key.
/// </summary>
public abstract class ResiliencePipelineProviderBase(
    ResiliencePipelineProvider<string> provider,
    string key)
{
    public ResiliencePipeline Get() => provider.GetPipeline(key);
}

