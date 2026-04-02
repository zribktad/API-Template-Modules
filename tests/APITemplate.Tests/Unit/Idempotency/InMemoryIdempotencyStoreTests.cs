using SharedKernel.Application.Contracts;
using SharedKernel.Infrastructure.Idempotency;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Idempotency;

public sealed class InMemoryIdempotencyStoreTests
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task TryAcquireAsync_WhenKeyIsNew_ReturnsLockToken()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl);

        token.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenLockAlreadyHeld_ReturnsNull()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? first = await store.TryAcquireAsync("key-1", DefaultTtl);
        string? second = await store.TryAcquireAsync("key-1", DefaultTtl);

        first.ShouldNotBeNull();
        second.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCachedResultExists_ReturnsNull()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl);
        token.ShouldNotBeNull();

        IdempotencyCacheEntry entry = new(200, """{"id":1}""", "application/json");
        await store.SetAsync("key-1", entry, DefaultTtl);
        await store.ReleaseAsync("key-1", token);

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl);

        secondToken.ShouldBeNull();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCachedResultExpired_ReturnsNewToken()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl);
        token.ShouldNotBeNull();

        IdempotencyCacheEntry entry = new(200, """{"id":1}""", "application/json");
        await store.SetAsync("key-1", entry, TimeSpan.FromSeconds(1));
        await store.ReleaseAsync("key-1", token);

        time.Advance(TimeSpan.FromMinutes(2));

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl);

        secondToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_WithCorrectToken_ReleasesLock()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl);
        token.ShouldNotBeNull();

        await store.ReleaseAsync("key-1", token);

        string? newToken = await store.TryAcquireAsync("key-1", DefaultTtl);
        newToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReleaseAsync_WithWrongToken_DoesNotReleaseLock()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        string? token = await store.TryAcquireAsync("key-1", DefaultTtl);
        token.ShouldNotBeNull();

        await store.ReleaseAsync("key-1", "wrong-token");

        string? secondToken = await store.TryAcquireAsync("key-1", DefaultTtl);
        secondToken.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsCachedEntry()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        IdempotencyCacheEntry entry = new(
            201,
            """{"id":42}""",
            "application/json",
            "/api/items/42"
        );
        await store.SetAsync("key-1", entry, DefaultTtl);

        IdempotencyCacheEntry? cached = await store.TryGetAsync("key-1");

        cached.ShouldNotBeNull();
        cached.StatusCode.ShouldBe(201);
        cached.ResponseBody.ShouldBe("""{"id":42}""");
        cached.ResponseContentType.ShouldBe("application/json");
        cached.LocationHeader.ShouldBe("/api/items/42");
    }

    [Fact]
    public async Task TryGetAsync_WhenExpired_ReturnsNull()
    {
        FakeTimeProvider time = new(DateTimeOffset.UtcNow);
        InMemoryIdempotencyStore store = new(time);

        IdempotencyCacheEntry entry = new(200, "{}", "application/json");
        await store.SetAsync("key-1", entry, TimeSpan.FromSeconds(1));

        time.Advance(TimeSpan.FromMinutes(2));

        IdempotencyCacheEntry? cached = await store.TryGetAsync("key-1");
        cached.ShouldBeNull();
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
