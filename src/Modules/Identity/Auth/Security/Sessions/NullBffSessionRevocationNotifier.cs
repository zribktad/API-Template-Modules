namespace Identity.Auth.Security.Sessions;

/// <summary>
///     No-op notifier used when Redis is not configured; revocation broadcasts become pure local
///     invalidations.
/// </summary>
public sealed class NullBffSessionRevocationNotifier : IBffSessionRevocationNotifier
{
    public Task PublishRevokedAsync(string sessionId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
