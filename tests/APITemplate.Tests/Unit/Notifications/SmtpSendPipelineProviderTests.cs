using BuildingBlocks.Application.Resilience;
using Notifications.Services;
using Polly;
using Polly.Registry;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Notifications;

[Trait("Category", "Unit")]
public sealed class SmtpSendPipelineProviderTests
{
    [Fact]
    public void Get_ReturnsNonNullPipeline()
    {
        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(ResiliencePipelineKeys.SmtpSend, (_, _) => { });

        SmtpSendPipelineProvider sut = new(registry);

        sut.Get().ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_ReturnedPipeline_ExecutesCallbackSuccessfully()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder(ResiliencePipelineKeys.SmtpSend, (_, _) => { });

        SmtpSendPipelineProvider sut = new(registry);
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
