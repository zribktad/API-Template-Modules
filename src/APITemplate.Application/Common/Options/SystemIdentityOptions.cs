using APITemplate.Domain.Entities;

namespace SharedKernel.Application.Options.Security;

/// <summary>
/// Configuration that defines the well-known actor identity used when the system performs
/// automated actions without an associated human user.
/// </summary>
public sealed class SystemIdentityOptions
{
    public Guid DefaultActorId { get; init; } = AuditDefaults.SystemActorId;
}
