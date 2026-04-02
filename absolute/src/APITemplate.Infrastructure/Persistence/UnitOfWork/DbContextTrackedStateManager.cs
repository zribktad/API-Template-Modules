using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace APITemplate.Infrastructure.Persistence;

/// <summary>
/// Captures and restores a snapshot of all non-detached EF Core change tracker entries,
/// enabling transactional rollback of in-memory state after a savepoint rollback.
/// </summary>
internal sealed class DbContextTrackedStateManager(AppDbContext dbContext)
{
    /// <summary>
    /// Captures the current state, current values, and original values of all tracked entities
    /// and returns a snapshot keyed by object reference identity.
    /// </summary>
    public IReadOnlyDictionary<object, TrackedEntitySnapshot> Capture()
    {
        return dbContext
            .ChangeTracker.Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .ToDictionary(
                entry => entry.Entity,
                entry => new TrackedEntitySnapshot(
                    entry.State,
                    entry.CurrentValues.Clone(),
                    entry.OriginalValues.Clone()
                ),
                ReferenceEqualityComparer.Instance
            );
    }

    /// <summary>
    /// Restores the change tracker to the given snapshot, detaching entities not present in
    /// the snapshot and reverting current/original values for those that are.
    /// </summary>
    public void Restore(IReadOnlyDictionary<object, TrackedEntitySnapshot> snapshot)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (!snapshot.TryGetValue(entry.Entity, out var entitySnapshot))
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(entitySnapshot.CurrentValues);
            entry.OriginalValues.SetValues(entitySnapshot.OriginalValues);
            entry.State = entitySnapshot.State;
        }
    }

    internal sealed record TrackedEntitySnapshot(
        EntityState State,
        PropertyValues CurrentValues,
        PropertyValues OriginalValues
    );
}
