namespace APITemplate.Domain.Entities.Contracts;

/// <summary>
/// Marks a domain entity as soft-deletable, meaning it is logically removed by setting <see cref="IsDeleted"/>
/// rather than being physically purged from the database.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedBy { get; set; }
}
