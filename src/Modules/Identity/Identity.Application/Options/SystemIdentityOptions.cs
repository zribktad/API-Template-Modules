using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SharedKernel.Domain.Entities;

namespace Identity.Application.Options;

/// <summary>
/// Configuration that defines the well-known actor identity used when the system performs
/// automated actions without an associated human user.
/// </summary>
public sealed class SystemIdentityOptions
{
    [Description("Actor identifier used for automated system actions without a human user.")]
    [Required]
    public Guid DefaultActorId { get; init; } = AuditDefaults.SystemActorId;
}
