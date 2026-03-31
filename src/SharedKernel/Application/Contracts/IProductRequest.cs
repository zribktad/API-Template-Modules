namespace SharedKernel.Application.Contracts;

/// <summary>
/// Shared contract for create and update product command requests, enabling reuse of
/// FluentValidation rules across both operations without duplicating property declarations.
/// </summary>
public interface IProductRequest
{
    string Name { get; }
    string? Description { get; }
    decimal Price { get; }
    Guid? CategoryId { get; }
    IReadOnlyCollection<Guid>? ProductDataIds { get; }
}
