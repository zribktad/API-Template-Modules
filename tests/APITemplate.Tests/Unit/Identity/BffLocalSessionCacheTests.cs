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
    public void Set_WhenDisabled_IsNoOp()
    {
        BffLocalSessionCache sut = CreateSut(ttlSeconds: 0);
        BffSessionRecord record = BffSessionStoreUnitTestHelpers.CreateSampleSession("abc");

        sut.Set("abc", record);

        sut.TryGet("abc", out BffSessionRecord? fetched).ShouldBeFalse();
        fetched.ShouldBeNull();
    }

    private static BffLocalSessionCache CreateSut(int ttlSeconds) =>
        new(Options.Create(new BffOptions { LocalCacheTtlSeconds = ttlSeconds }));
}
