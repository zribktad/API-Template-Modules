using ErrorOr;

namespace Identity.Directory.Features.Role.Shared;

public static class RoleLoader
{
    public static async Task<ErrorOr<CustomRole>> LoadMutableAsync(
        Guid roleId,
        Guid tenantId,
        IRoleRepository repository,
        CancellationToken ct
    )
    {
        CustomRole? role = await repository.FirstOrDefaultAsync(
            new RoleByIdSpecification(roleId),
            ct
        );
        if (role == null)
            return DomainErrors.Roles.NotFound(roleId);
        if (role.IsImmutable)
            return DomainErrors.Roles.Immutable();
        if (role.TenantId != tenantId)
            return DomainErrors.Roles.NotFound(roleId);
        return role;
    }
}
