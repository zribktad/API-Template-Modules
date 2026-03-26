using APITemplate.Application.Common.Startup;

namespace APITemplate.Tests.Helpers;

internal sealed class TestNoOpStartupTaskCoordinator : IStartupTaskCoordinator
{
    public Task<IAsyncDisposable> AcquireAsync(
        StartupTaskName startupTask,
        CancellationToken ct = default
    ) => Task.FromResult<IAsyncDisposable>(NoOpAsyncDisposable.Instance);

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public static NoOpAsyncDisposable Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
