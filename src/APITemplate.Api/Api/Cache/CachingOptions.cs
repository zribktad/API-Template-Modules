using System.ComponentModel.DataAnnotations;

namespace APITemplate.Api.Cache;

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
}
