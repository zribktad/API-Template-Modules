namespace SharedKernel.Domain.Entities.Contracts;

/// <summary>
///     Marks a domain entity as soft-deletable, meaning it is logically removed by setting <see cref="IsDeleted" />
///     rather than being physically purged from the database.
/// </summary>
public interface ISoftDeletable
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
