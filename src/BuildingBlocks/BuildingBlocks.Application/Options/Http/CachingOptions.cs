using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Application.Options.Http;

public sealed class CachingOptions : IModuleOptions
{
    public static string SectionName => "Caching";

    [Range(1, int.MaxValue)]
    public int ProductsExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int CategoriesExpirationSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int ReviewsExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int ProductDataExpirationSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int TenantsExpirationSeconds { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int TenantInvitationsExpirationSeconds { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int UsersExpirationSeconds { get; set; } = 300;
}
