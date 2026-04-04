using SharedKernel.Application.Contracts;
using SharedKernel.Infrastructure.Idempotency;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Idempotency;

/// <summary>
///     Covers in-process idempotency. <see cref="SharedKernel.Infrastructure.Idempotency.DistributedCacheIdempotencyStore" /> depends on Redis Lua scripts;
///     add container-backed integration tests if multi-instance lock semantics need automated coverage.
/// </summary>
public sealed class InMemoryIdempotencyStoreTests
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    [Theory]
    [InlineData("key-1")]
    [InlineData("tenant:scope:123")]
    [InlineData("")]
    public async Task TryAcquireAsync_WhenKeyIsNew_ReturnsLockToken(string key)
    {
        (InMemoryIdempotencyStore store, CancellationToken ct) = CreateStore();

        string? token = await store.TryAcquireAsync(key, DefaultTtl, ct);

        token.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3600000)]
    public async Task TryAcquireAsync_VariousTtlsStillAcquires(int ttlMs)
    {
        (InMemoryIdempotencyStore store, CancellationToken ct) = CreateStore();
        string key = Guid.NewGuid().ToString("N");

        string? token = await store.TryAcquireAsync(key, TimeSpan.FromMilliseconds(ttlMs), ct);

        token.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLockAlreadyHeld_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? first = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        string? second = await store.TryAcquireAsync("key-1", DefaultTtl, ct);

        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCachedResultExists_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        token.ShouldNotBeNull();

        IdempotencyCacheEntry entry = new(200, """{"id":1}""", "application/json");
        await store.SetAsync("key-1", entry, DefaultTtl, ct);
        await store.ReleaseAsync("key-1", token!, ct);

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl, ct);

        secondToken.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCachedResultExpired_ReturnsNewToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        token.ShouldNotBeNull();

        IdempotencyCacheEntry entry = new(200, """{"id":1}""", "application/json");
        await store.SetAsync("key-1", entry, TimeSpan.FromSeconds(1), ct);
        await store.ReleaseAsync("key-1", token!, ct);

        time.Advance(TimeSpan.FromMinutes(2));

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl, ct);

        secondToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_WithCorrectToken_ReleasesLock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        token.ShouldNotBeNull();

        await store.ReleaseAsync("key-1", token!, ct);

        string? newToken = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        newToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_WithWrongToken_DoesNotReleaseLock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        token.ShouldNotBeNull();

        await store.ReleaseAsync("key-1", "wrong-token", ct);

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl, ct);
        secondToken.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsCachedEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        IdempotencyCacheEntry entry = new(
            201,
            """{"id":42}""",
            "application/json",
            "/api/items/42"
        );
        await store.SetAsync("key-1", entry, DefaultTtl, ct);

        IdempotencyCacheEntry? cached = await store.TryGetAsync("key-1", ct);

        cached.ShouldNotBeNull();
        cached.StatusCode.ShouldBe(201);
        cached.ResponseBody.ShouldBe("""{"id":42}""");
        cached.ResponseContentType.ShouldBe("application/json");
        cached.LocationHeader.ShouldBe("/api/items/42");
    }

    [Fact]
    public async Task TryGetAsync_WhenExpired_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        IdempotencyCacheEntry entry = new(200, "{}", "application/json");
        await store.SetAsync("key-1", entry, TimeSpan.FromSeconds(1), ct);

        time.Advance(TimeSpan.FromMinutes(2));

        IdempotencyCacheEntry? cached = await store.TryGetAsync("key-1", ct);
        cached.ShouldBeNull();
    }

    private static (InMemoryIdempotencyStore Store, CancellationToken Ct) CreateStore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        return (new InMemoryIdempotencyStore(time), ct);
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
