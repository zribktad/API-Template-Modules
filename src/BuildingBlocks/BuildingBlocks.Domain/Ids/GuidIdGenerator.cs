using BuildingBlocks.Domain.Interfaces;

namespace BuildingBlocks.Domain.Ids;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
