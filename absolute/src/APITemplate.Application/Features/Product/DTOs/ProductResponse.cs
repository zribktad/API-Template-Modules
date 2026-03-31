namespace APITemplate.Application.Features.Product.DTOs;

/// <summary>
/// Represents a product as returned by the Application layer to API consumers, projected from the domain entity.
/// </summary>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid? CategoryId,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<Guid> ProductDataIds
) : IHasId;
