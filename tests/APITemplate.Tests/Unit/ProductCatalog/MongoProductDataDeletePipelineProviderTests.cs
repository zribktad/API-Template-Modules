using Polly;
using Polly.Registry;
using ProductCatalog.Services;
using SharedKernel.Application.Resilience;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ProductCatalog;

[Trait("Category", "Unit")]
public sealed class MongoProductDataDeletePipelineProviderTests
{
    [Fact]
    public void Get_ReturnsNonNullPipeline()
    {
        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(ResiliencePipelineKeys.MongoProductDataDelete, (_, _) => { });

        MongoProductDataDeletePipelineProvider sut = new(registry);

        sut.Get().ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_ReturnedPipeline_ExecutesCallbackSuccessfully()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(ResiliencePipelineKeys.MongoProductDataDelete, (_, _) => { });

        MongoProductDataDeletePipelineProvider sut = new(registry);
        bool executed = false;

        await sut.Get()
            .ExecuteAsync(
                _ =>
                {
                    executed = true;
                    return ValueTask.CompletedTask;
                },
                ct
            );

        executed.ShouldBeTrue();
    }
}
