using System.Text.Json.Serialization;

namespace APITemplate.Application.Features.ProductData.DTOs;

/// <summary>
/// Abstract base read model for product data, serialised as a polymorphic type using the <c>type</c> discriminator.
/// Concrete subtypes add media-specific properties.
/// </summary>
[JsonDerivedType(typeof(ImageProductDataResponse), "image")]
[JsonDerivedType(typeof(VideoProductDataResponse), "video")]
public abstract record ProductDataResponse : IHasId
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Format { get; init; }
    public long? FileSizeBytes { get; init; }
}

/// <summary>
/// Read model for image product data, extending <see cref="ProductDataResponse"/> with pixel dimensions.
/// </summary>
public sealed record ImageProductDataResponse : ProductDataResponse
{
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// Read model for video product data, extending <see cref="ProductDataResponse"/> with duration and resolution.
/// </summary>
public sealed record VideoProductDataResponse : ProductDataResponse
{
    public int DurationSeconds { get; init; }
    public string? Resolution { get; init; }
}
