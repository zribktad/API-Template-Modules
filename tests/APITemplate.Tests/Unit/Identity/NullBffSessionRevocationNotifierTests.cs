using Identity.Auth.Security.Sessions;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class NullBffSessionRevocationNotifierTests
{
    [Fact]
    public void PublishRevokedAsync_ReturnsCompletedTaskSynchronously()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        NullBffSessionRevocationNotifier sut = new();

        Task task = sut.PublishRevokedAsync("s1", ct);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishRevokedAsync_WithCancelledToken_DoesNotThrow()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        NullBffSessionRevocationNotifier sut = new();

        await Should.NotThrowAsync(async () => await sut.PublishRevokedAsync("s1", cts.Token));
    }
}
