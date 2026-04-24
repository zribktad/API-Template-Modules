using Identity.Auth.Options;
using Identity.Auth.Security.Sessions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class BffLocalSessionCacheTests
{
    [Fact]
    public void Set_ThenTryGet_ReturnsStoredRecord()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");

        sut.Set("abc", record);

        sut.TryGet("abc", out BffSessionRecord? fetched).ShouldBeTrue();
        fetched.ShouldBe(record);
    }

    [Fact]
    public void TryGet_WhenMissing_ReturnsFalse()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);

        sut.TryGet("missing", out BffSessionRecord? fetched).ShouldBeFalse();
        fetched.ShouldBeNull();
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");
        sut.Set("abc", record);

        sut.Invalidate("abc");

        sut.TryGet("abc", out _).ShouldBeFalse();
    }

    [Fact]
    public void Invalidate_BumpsGeneration()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        long before = sut.Generation;

        sut.Invalidate("abc");

        sut.Generation.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void Set_OverwritesPreviousEntry_DoesNotBumpGeneration()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        BffSessionRecord first = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");
        BffSessionRecord second = first with { Email = "updated@example.com" };
        long before = sut.Generation;

        sut.Set("abc", first);
        sut.Set("abc", second);
        sut.TryGet("abc", out BffSessionRecord? fetched).ShouldBeTrue();

        fetched.ShouldBe(second);
        sut.Generation.ShouldBe(before);
    }

    [Fact]
    public void Invalidate_WhenKeyMissing_StillBumpsGeneration()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        long before = sut.Generation;

        sut.Invalidate("missing");

        sut.Generation.ShouldBe(before + 1);
    }

    [Fact]
    [Trait("Speed", "Slow")]
    public async Task TryGet_AfterTtlElapses_ReturnsFalse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 1);
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");

        sut.Set("abc", record);
        await Task.Delay(TimeSpan.FromMilliseconds(1100), ct);

        sut.TryGet("abc", out BffSessionRecord? fetched).ShouldBeFalse();
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task Set_WhenExceedsSizeLimit_EvictsEntries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30, maxEntries: 2);

        sut.Set("s1", BffSessionStoreUnitTestHelpers.CreateSampleSession("s1"));
        sut.Set("s2", BffSessionStoreUnitTestHelpers.CreateSampleSession("s2"));
        sut.Set("s3", BffSessionStoreUnitTestHelpers.CreateSampleSession("s3"));

        bool entryEvicted = await BffSessionStoreUnitTestHelpers.WaitUntilAsync(
            () => !sut.TryGet("s1", out _) || !sut.TryGet("s2", out _) || !sut.TryGet("s3", out _),
            ct,
            TimeSpan.FromMilliseconds(50)
        );

        entryEvicted.ShouldBeTrue();
    }

    [Fact]
    public void Generation_UnderParallelInvalidate_IsMonotonicAndAtomic()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 30);
        long before = sut.Generation;

        Parallel.For(0, 1000, _ => sut.Invalidate("abc"));

        sut.Generation.ShouldBe(before + 1000);
    }

    [Fact]
    public void Set_WhenDisabled_IsNoOp()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 0);
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");

        sut.Set("abc", record);

        sut.TryGet("abc", out BffSessionRecord? fetched).ShouldBeFalse();
        fetched.ShouldBeNull();
    }

    [Fact]
    public void Invalidate_WhenCacheDisabled_DoesNotBumpGeneration()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 0);
        long before = sut.Generation;

        sut.Invalidate("abc");

        sut.Generation.ShouldBe(before);
    }

    private static BffLocalSessionCache CreateSut(int ttlSeconds, int maxEntries = 10) =>
        new(
            Options.Create(
                new BffOptions
                {
                    LocalCacheTtlSeconds = ttlSeconds,
                    LocalCacheMaxEntries = maxEntries,
                }
            )
        );
}
