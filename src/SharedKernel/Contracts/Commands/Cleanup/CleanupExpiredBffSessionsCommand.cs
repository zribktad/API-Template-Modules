namespace SharedKernel.Contracts.Commands.Cleanup;

/// <summary>
///     Cross-module command instructing the Identity module to purge expired, revoked, and
///     idle BFF sessions. Dispatched by the BackgroundJobs cleanup orchestrator via the message bus.
/// </summary>
public sealed record CleanupExpiredBffSessionsCommand(int BatchSize);
