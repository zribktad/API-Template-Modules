using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SharedKernel.Infrastructure.UnitOfWork;

/// <summary>
///     Captures and restores a snapshot of all non-detached EF Core change tracker entries.
/// </summary>
internal sealed class DbContextTrackedStateManager(DbContext dbContext)
{
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

    public void Restore(IReadOnlyDictionary<object, TrackedEntitySnapshot> snapshot)
    {
        foreach (EntityEntry entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (!snapshot.TryGetValue(entry.Entity, out TrackedEntitySnapshot? entitySnapshot))
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
