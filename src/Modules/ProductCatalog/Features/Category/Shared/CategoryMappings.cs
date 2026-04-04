using System.Linq.Expressions;
using CategoryEntity = ProductCatalog.Entities.Category;

namespace ProductCatalog.Features.Category.Shared;

/// <summary>
/// Provides mapping utilities between category domain entities and their response DTOs.
/// The compiled projection is reused across specifications and in-memory conversions for consistency.
/// </summary>
public static class CategoryMappings
{
    /// <summary>
    /// EF Core-compatible expression that projects a <see cref="CategoryEntity"/> to a <see cref="CategoryResponse"/>.
    /// Shared with <see cref="Specifications"/> so the same shape is produced by both DB queries and in-memory maps.
    /// </summary>
    public static readonly Expression<Func<CategoryEntity, CategoryResponse>> Projection =
        category => new CategoryResponse(
            category.Id,
            category.Name,
            category.Description,
            category.Audit.CreatedAtUtc
        );

    private static readonly Func<CategoryEntity, CategoryResponse> CompiledProjection =
        Projection.Compile();

    /// <summary>Maps a <see cref="CategoryEntity"/> to a <see cref="CategoryResponse"/> using the compiled projection.</summary>
    public static CategoryResponse ToResponse(this CategoryEntity category) =>
        CompiledProjection(category);
}
