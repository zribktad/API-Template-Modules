namespace SharedKernel.Contracts.Commands.Cleanup;

/// <summary>
/// Cross-module command instructing the Identity module to purge expired tenant invitations.
/// Dispatched by the BackgroundJobs cleanup orchestrator via the message bus.
/// </summary>
public sealed record CleanupExpiredInvitationsCommand(int RetentionHours, int BatchSize);
