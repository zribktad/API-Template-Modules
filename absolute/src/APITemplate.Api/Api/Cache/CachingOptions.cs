using System.ComponentModel.DataAnnotations;

namespace APITemplate.Api.Cache;

/// <summary>
/// Strongly-typed options model for configuring per-resource output cache expiration durations.
/// Bound from the <c>Caching</c> configuration section and validated on startup.
/// </summary>
public sealed class CachingOptions
{
    [Range(1, int.MaxValue)]
    public int ProductsExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int CategoriesExpirationSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int ReviewsExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int ProductDataExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int TenantsExpirationSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int TenantInvitationsExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int UsersExpirationSeconds { get; set; } = 30;
}
