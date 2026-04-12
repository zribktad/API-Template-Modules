namespace Identity.Directory.Entities;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public CustomRole Role { get; set; } = null!;

    public required string Permission { get; set; }
}
