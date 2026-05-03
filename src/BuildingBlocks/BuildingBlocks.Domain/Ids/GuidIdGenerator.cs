using BuildingBlocks.Domain.Interfaces;

namespace SharedKernel.Infrastructure.Ids;

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}

