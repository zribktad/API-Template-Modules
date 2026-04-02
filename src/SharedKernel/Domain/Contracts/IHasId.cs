namespace SharedKernel.Domain.Entities.Contracts;

/// <summary>
/// Marks a type that carries a unique <see cref="Guid"/> identity.
/// </summary>
public interface IHasId
{
    Guid Id { get; }
}
