using ImageProductDataEntity = APITemplate.Domain.Entities.ProductData.ImageProductData;
using ProductDataEntity = APITemplate.Domain.Entities.ProductData.ProductData;
using VideoProductDataEntity = APITemplate.Domain.Entities.ProductData.VideoProductData;

namespace APITemplate.Application.Features.ProductData.Mappings;

/// <summary>
/// Provides mapping utilities from product data domain entities to their polymorphic response DTOs.
/// Dispatches to a type-specific mapping method based on the concrete entity type.
/// </summary>
public static class ProductDataMappings
{
    /// <summary>
    /// Maps a <see cref="ProductDataEntity"/> to the appropriate <see cref="ProductDataResponse"/> subtype.
    /// Throws <see cref="InvalidOperationException"/> for unrecognised entity types.
    /// </summary>
    public static ProductDataResponse ToResponse(this ProductDataEntity data) =>
        data switch
        {
            ImageProductDataEntity image => image.ToImageResponse(),
            VideoProductDataEntity video => video.ToVideoResponse(),
            _ => throw new InvalidOperationException(
                $"Unknown ProductData type: {data.GetType().Name}"
            ),
        };

    /// <summary>Copies shared fields from the base entity onto an already-populated response record.</summary>
    private static T MapCommon<T>(this ProductDataEntity data, T response, string type)
        where T : ProductDataResponse =>
        response with
        {
            Id = data.Id,
            Title = data.Title,
            Description = data.Description,
            CreatedAt = data.CreatedAt,
            Type = type,
        };

    /// <summary>Maps an <see cref="ImageProductDataEntity"/> to an <see cref="ImageProductDataResponse"/>.</summary>
    private static ImageProductDataResponse ToImageResponse(this ImageProductDataEntity image) =>
        image.MapCommon(
            new ImageProductDataResponse
            {
                Width = image.Width,
                Height = image.Height,
                Format = image.Format,
                FileSizeBytes = image.FileSizeBytes,
            },
            "image"
        );

    /// <summary>Maps a <see cref="VideoProductDataEntity"/> to a <see cref="VideoProductDataResponse"/>.</summary>
    private static VideoProductDataResponse ToVideoResponse(this VideoProductDataEntity video) =>
        video.MapCommon(
            new VideoProductDataResponse
            {
                DurationSeconds = video.DurationSeconds,
                Resolution = video.Resolution,
                Format = video.Format,
                FileSizeBytes = video.FileSizeBytes,
            },
            "video"
        );
}
