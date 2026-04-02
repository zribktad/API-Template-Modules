namespace SharedKernel.Domain.Entities;

/// <summary>
/// Provides well-known sentinel values used to populate <see cref="AuditInfo"/> when no real actor is available.
/// </summary>
public static class AuditDefaults
{
    /// <summary>
    /// The actor ID assigned to audit fields when an operation is performed by the system rather than a human user.
    /// </summary>
    public static readonly Guid SystemActorId = Guid.Empty;
}
