namespace APITemplate.Tests.Unit.Helpers;

/// <summary>Fixed UTC clock for tests that only need a stable <see cref="TimeProvider.GetUtcNow" />.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

/// <summary>UTC clock that tests can advance (e.g. TTL / expiry scenarios).</summary>
internal sealed class MutableFakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public MutableFakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
