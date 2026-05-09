using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BuildingBlocks.Application.Options;

namespace Identity.Directory.Options;

/// <summary>
///     Configuration for tenant invitation flows, including token lifetime and the application base URL
///     used when generating invitation links.
/// </summary>
public sealed class TenantInvitationOptions : IModuleOptions
{
    public static string SectionName => "TenantInvitation";

    [Description("Lifetime of invitation tokens, in hours, for email-driven onboarding flows.")]
    [Range(1, int.MaxValue)]
    public int InvitationTokenExpiryHours { get; set; } = 72;

    [Description("Public application base URL used when generating invitation links in emails.")]
    [Required]
    [MinLength(1)]
    public string BaseUrl { get; set; } = string.Empty;
}
