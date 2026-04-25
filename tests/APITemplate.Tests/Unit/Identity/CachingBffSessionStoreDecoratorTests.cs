using System.Collections.Concurrent;
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
        _localCache.SetupGet(c => c.Generation).Returns(7L);
        _inner.Setup(i => i.GetAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(record);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBe(record);
        _localCache.Verify(c => c.Set("s1", record), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenCacheGenerationChangesDuringFetch_ReturnsFreshRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord staleRecord = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord freshRecord = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Version = 2,
        };
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _localCache
            .SetupSequence(c => c.Generation)
            .Returns(7L)
            .Returns(8L)
            .Returns(8L)
            .Returns(8L);
        _inner
            .SetupSequence(i => i.GetAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleRecord)
            .ReturnsAsync(freshRecord);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBe(freshRecord);
        _localCache.Verify(c => c.Set("s1", freshRecord), Times.Once);
        _inner.Verify(i => i.GetAsync("s1", CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_GenerationChangedDuringBackgroundFetch_ReturnsFreshRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource<BffSessionRecord?> fetchGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        BffSessionRecord staleRecord = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord freshRecord = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Version = 2,
        };
        BffSessionRecord? outRecord = null;
        long generation = 7;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _localCache.SetupGet(c => c.Generation).Returns(() => generation);
        _inner
            .SetupSequence(i => i.GetAsync("s1", It.IsAny<CancellationToken>()))
            .Returns(fetchGate.Task)
            .ReturnsAsync(freshRecord);

        CachingBffSessionStoreDecorator sut = CreateSut();

        Task<BffSessionRecord?> fetch = sut.GetAsync("s1", ct);
        await BffSessionStoreUnitTestHelpers.WaitUntilAsync(
            () =>
                _inner.Invocations.Count(invocation =>
                    invocation.Method.Name == nameof(IBffSessionStore.GetAsync)
                ) == 1,
            ct
        );
        generation = 8;
        fetchGate.SetResult(staleRecord);

        BffSessionRecord? result = await fetch;

        result.ShouldBe(freshRecord);
        _localCache.Verify(c => c.Set("s1", freshRecord), Times.Once);
        _inner.Verify(i => i.GetAsync("s1", CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_ConcurrentCallers_InnerCalledOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TaskCompletionSource<BffSessionRecord?> fetchGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _localCache.SetupGet(c => c.Generation).Returns(1);
        _inner.Setup(i => i.GetAsync("s1", It.IsAny<CancellationToken>())).Returns(fetchGate.Task);

        CachingBffSessionStoreDecorator sut = CreateSut();

        Task<BffSessionRecord?>[] callers = Enumerable
            .Range(0, 20)
            .Select(_ => Task.Run(() => sut.GetAsync("s1", ct), ct))
            .ToArray();

        await BffSessionStoreUnitTestHelpers.WaitUntilAsync(
            () =>
                _inner.Invocations.Count(invocation =>
                    invocation.Method.Name == nameof(IBffSessionStore.GetAsync)
                ) == 1,
            ct
        );
        fetchGate.SetResult(record);

        BffSessionRecord?[] results = await Task.WhenAll(callers);

        results.ShouldAllBe(result => result == record);
        _inner.Verify(i => i.GetAsync("s1", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenInnerThrows_StampedeSlotReleased()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _localCache.SetupGet(c => c.Generation).Returns(1);
        _inner
            .SetupSequence(i => i.GetAsync("s1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(record);

        CachingBffSessionStoreDecorator sut = CreateSut();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.GetAsync("s1", ct)
        );
        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBe(record);
        _inner.Verify(i => i.GetAsync("s1", CancellationToken.None), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_WhenLocalMissAndInnerReturnsNull_DoesNotPopulate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord? outRecord = null;
        _localCache.Setup(c => c.TryGet("s1", out outRecord)).Returns(false);
        _inner
            .Setup(i => i.GetAsync("s1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionRecord?)null);

        CachingBffSessionStoreDecorator sut = CreateSut();

        BffSessionRecord? result = await sut.GetAsync("s1", ct);

        result.ShouldBeNull();
        _localCache.Verify(
            c => c.Set(It.IsAny<string>(), It.IsAny<BffSessionRecord>()),
            Times.Never
        );
    }

    [Fact]
    public async Task StoreAsync_WhenActive_WritesThroughToLocalCacheWithoutPublish()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Active,
        };

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
    public async Task StoreAsync_WhenTerminal_InvalidatesAndPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1") with
        {
            Status = BffSessionStatus.Revoked,
        };

        CachingBffSessionStoreDecorator sut = CreateSut();

        await sut.StoreAsync(record, ct);

        _inner.Verify(i => i.StoreAsync(record, ct), Times.Once);
        _localCache.Verify(c => c.Invalidate("s1"), Times.Once);
        _localCache.Verify(
            c => c.Set(It.IsAny<string>(), It.IsAny<BffSessionRecord>()),
            Times.Never
        );
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WhenCallerCtCancelled_PublishStillUsesNone()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateTerminalSession("s1");

        CachingBffSessionStoreDecorator sut = CreateSut();

        await sut.StoreAsync(record, cts.Token);

        _inner.Verify(i => i.StoreAsync(record, cts.Token), Times.Once);
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_WhenInnerThrows_DoesNotTouchCacheOrNotifier()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateTerminalSession("s1");
        _inner
            .Setup(i => i.StoreAsync(record, ct))
            .ThrowsAsync(new InvalidOperationException("boom"));

        CachingBffSessionStoreDecorator sut = CreateSut();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await sut.StoreAsync(record, ct)
        );

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
    public async Task TryUpdateAsync_WhenActive_WritesThroughAndPublishes()
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
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
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
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
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
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
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
    public async Task TryUpdateAsync_WhenPublishThrows_DoesNotPropagate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("s1");
        _inner.Setup(i => i.TryUpdateAsync(record, 5, ct)).ReturnsAsync(true);
        _notifier
            .Setup(n => n.PublishRevokedAsync("s1", CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("publish failed"));

        CachingBffSessionStoreDecorator sut = CreateSut();

        bool updated = await sut.TryUpdateAsync(record, 5, ct);

        updated.ShouldBeTrue();
        _localCache.Verify(c => c.Set("s1", record), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_InvalidatesAndPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        CachingBffSessionStoreDecorator sut = CreateSut();

        await sut.RemoveAsync("s1", ct);

        _inner.Verify(i => i.RemoveAsync("s1", ct), Times.Once);
        _localCache.Verify(c => c.Invalidate("s1"), Times.Once);
        _notifier.Verify(n => n.PublishRevokedAsync("s1", CancellationToken.None), Times.Once);
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
            _notifier.Verify(n => n.PublishRevokedAsync(id, CancellationToken.None), Times.Once);
        }
    }

    [Fact]
    public async Task BulkRevoke_WhenEmptyResult_NoInvalidationsNorPublishes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _inner
            .Setup(i =>
                i.BulkRevokeActiveSessionsBySubjectAsync(
                    "sub",
                    BffSessionRevocationReason.CredentialRotation,
                    It.IsAny<DateTimeOffset>(),
                    ct
                )
            )
            .ReturnsAsync(Array.Empty<string>());

        CachingBffSessionStoreDecorator sut = CreateSut();

        IReadOnlyList<string> result = await sut.BulkRevokeActiveSessionsBySubjectAsync(
            "sub",
            BffSessionRevocationReason.CredentialRotation,
            DateTimeOffset.UtcNow,
            ct
        );

        result.ShouldBeEmpty();
        _localCache.Verify(c => c.Invalidate(It.IsAny<string>()), Times.Never);
        _notifier.Verify(
            n => n.PublishRevokedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task BulkRevoke_PublishesConcurrently_NotSequentially()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string[] ids = ["s1", "s2", "s3"];
        ConcurrentDictionary<string, TaskCompletionSource> publishGates = new();
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
        _notifier
            .Setup(n => n.PublishRevokedAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns<string, CancellationToken>(
                (id, _) =>
                {
                    TaskCompletionSource gate = new(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );
                    publishGates[id] = gate;
                    return gate.Task;
                }
            );

        CachingBffSessionStoreDecorator sut = CreateSut();

        Task<IReadOnlyList<string>> bulkRevoke = sut.BulkRevokeActiveSessionsBySubjectAsync(
            "sub",
            BffSessionRevocationReason.CredentialRotation,
            DateTimeOffset.UtcNow,
            ct
        );

        bool allPublishTasksStarted = await BffSessionStoreUnitTestHelpers.WaitUntilAsync(
            () => publishGates.Count == ids.Length,
            ct
        );
        allPublishTasksStarted.ShouldBeTrue();
        foreach (TaskCompletionSource gate in publishGates.Values)
            gate.SetResult();

        IReadOnlyList<string> result = await bulkRevoke;

        result.ShouldBe(ids);
    }

    private CachingBffSessionStoreDecorator CreateSut() =>
        new(_inner.Object, _localCache.Object, _notifier.Object);
}
