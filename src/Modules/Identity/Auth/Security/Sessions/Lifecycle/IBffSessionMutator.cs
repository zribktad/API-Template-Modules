namespace Identity.Auth.Security.Sessions.Lifecycle;

public interface IBffSessionMutator
{
    Task MutateAsync(
        string sessionId,
        Func<BffSessionRecord, BffSessionRecord> mutate,
        CancellationToken ct = default
    );
}
