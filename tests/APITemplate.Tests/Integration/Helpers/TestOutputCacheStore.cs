using System.Collections.Concurrent;
using Microsoft.AspNetCore.OutputCaching;

namespace APITemplate.Tests.Integration.Helpers;

internal sealed class TestOutputCacheStore : IOutputCacheStore
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tags = new(
        StringComparer.Ordinal
    );

    public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_entries.TryGetValue(key, out var value) ? value : null);
    }

    public ValueTask SetAsync(
        string key,
        byte[] value,
        string[]? tags,
        TimeSpan validFor,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        _entries[key] = value;
        foreach (var tag in (tags ?? []).Distinct(StringComparer.Ordinal))
        {
            var keys = _tags.GetOrAdd(tag, _ => new(StringComparer.Ordinal));
            keys[key] = 0;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_tags.TryRemove(tag, out var keys))
            return ValueTask.CompletedTask;

        foreach (var key in keys.Keys)
            _entries.TryRemove(key, out _);

        return ValueTask.CompletedTask;
    }
}
