using Identity.Auth.Security.Sessions;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class CachingBffSessionStoreDecoratorTests
{
    private readonly Mock<IBffSessionStore> _inner = new();
    private readonly Mock<IBffLocalSessionCache> _localCache = new();
    private readonly Mock<IBffSessionRevocationNotifier> _notifier = new();

    [Fact]
    public async Task GetAsync_WhenLocalHit_SkipsInner()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord cached = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord? outRecord = cached;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(true);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBe(cached);
        _inner.Verify(
            i => i.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetAsync_WhenLocalMiss_PopulatesLocalAfterInnerFetch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _inner.Setup(i => i.GetAsync("s1", ct)).ReturnsAsync(record);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBe(record);
        _localCache.Verify(c => c.Set("s1", record), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenLocalMissAndInnerReturnsNull_DoesNotPopulate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _inner.Setup(i => i.GetAsync("s1", ct)).ReturnsAsync((BffSessionRecord?)null);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBeNull();
        _localCache.Verify(
            c => c.Set(It.IsAny<string>(), It.IsAny<BffSessionRecord>()),
            Times.Never
        );
    }

    [Fact]
    public async Task StoreAsync_WriteThroughToLocalCache()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");

        CachingBffSessionStoreDecorator sut = CreateSut();

        await sut.StoreAsync(record, ct);

        _inner.Verify(i => i.StoreAsync(record, ct), Times.Once);
        _localCache.Verify(c => c.Set("s1", record), Times.Once);
        _notifier.Verify(
            n => n.PublishRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task TryUpdateAsync_WhenActive_WritesThroughWithoutPublish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Active,
        };
        _inner.Setup(i => i.TryUpdateAsync(record, 5, ct)).ReturnsAsync(true);

        CachingBffSessionStoreDecorator sut = CreateSut();

        bool updated = await sut.TryUpdateAsync(record, 5, ct);

        updated.ShouldBeTrue();
        _localCache.Verify(c => c.Set("s1", record), Times.Once);
        _localCache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        _notifier.Verify(
            n => n.PublishRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task TryUpdateAsync_WhenRevoked_InvalidatesAndPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Revoked,
        };
        _inner.Setup(i => i.TryUpdateAsync(record, 5, ct)).ReturnsAsync(true);

        CachingBffSessionStoreDecorator sut = CreateSut();

        bool updated = await sut.TryUpdateAsync(record, 5, ct);

        updated.ShouldBeTrue();
        _localCache.Verify(c => c.Invalidate("s1"), Times.Once);
        _localCache.Verify(
            c => c.Set(It.IsAny<string>(), It.IsAny<BffSessionRecord>()),
            Times.Never
        );
        _notifier.Verify(n => n.PublishRevokedAsync("s1", ct), Times.Once);
    }

    [Fact]
    public async Task TryUpdateAsync_WhenExpired_InvalidatesAndPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Expired,
        };
        _inner.Setup(i => i.TryUpdateAsync(record, 5, ct)).ReturnsAsync(true);

        CachingBffSessionStoreDecorator sut = CreateSut();

        bool updated = await sut.TryUpdateAsync(record, 5, ct);

        updated.ShouldBeTrue();
        _localCache.Verify(c => c.Invalidate("s1"), Times.Once);
        _notifier.Verify(n => n.PublishRevokedAsync("s1", ct), Times.Once);
    }

    [Fact]
    public async Task TryUpdateAsync_WhenInnerReturnsFalse_DoesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Revoked,
        };
        _inner.Setup(i => i.TryUpdateAsync(record, 5, ct)).ReturnsAsync(false);

        CachingBffSessionStoreDecorator sut = CreateSut();

        bool updated = await sut.TryUpdateAsync(record, 5, ct);

        updated.ShouldBeFalse();
        _localCache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        _localCache.Verify(
            c => c.Set(It.IsAny<string>(), It.IsAny<BffSessionRecord>()),
            Times.Never
        );
        _notifier.Verify(
            n => n.PublishRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RemoveAsync_InvalidatesAndPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        CachingBffSessionStoreDecorator sut = CreateSut();

        await sut.RemoveAsync("s1", ct);

        _inner.Verify(i => i.RemoveAsync("s1", ct), Times.Once);
        _localCache.Verify(c => c.Invalidate("s1"), Times.Once);
        _notifier.Verify(n => n.PublishRevokedAsync("s1", ct), Times.Once);
    }

    [Fact]
    public async Task BulkRevokeActiveSessionsBySubjectAsync_InvalidatesAndPublishesEachId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> ids = ["s1", "s2", "s3"];
        _inner
            .Setup(i =>
                i.BulkRevokeActiveSessionsBySubjectAsync(
                    "sub",
                    BffSessionRevocationReason.CredentialRotation,
                    It.IsAny<DateTimeOffset>(),
                    ct
                )
            )
            .ReturnsAsync(ids);

        CachingBffSessionStoreDecorator sut = CreateSut();

        IReadOnlyList<string> result = await sut.BulkRevokeActiveSessionsBySubjectAsync(
            "sub",
            BffSessionRevocationReason.CredentialRotation,
            DateTimeOffset.UtcNow,
            ct
        );

        result.ShouldBe(ids);
        foreach (string id in ids)
        {
            _localCache.Verify(c => c.Invalidate(id), Times.Once);
            _notifier.Verify(n => n.PublishRevokedAsync(id, ct), Times.Once);
        }
    }

    private CachingBffSessionStoreDecorator CreateSut() =>
        new(_inner.Object, _localCache.Object, _notifier.Object);
}
