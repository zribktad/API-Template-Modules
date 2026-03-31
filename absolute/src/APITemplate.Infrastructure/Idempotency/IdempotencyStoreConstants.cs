namespace APITemplate.Infrastructure.Idempotency;

/// <summary>Shared key-naming constants used by <see cref="InMemoryIdempotencyStore"/> and <see cref="DistributedCacheIdempotencyStore"/>.</summary>
internal static class IdempotencyStoreConstants
{
    /// <summary>Suffix appended to an idempotency key to form the corresponding distributed-lock key.</summary>
    public const string LockSuffix = ":lock";
}
