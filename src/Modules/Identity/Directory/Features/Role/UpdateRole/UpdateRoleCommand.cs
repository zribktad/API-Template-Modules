using ErrorOr;
using FluentValidation;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.InvalidatePermissions;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Interfaces;
using Microsoft.AspNetCore.Http;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
using Wolverine;

namespace Identity.Directory.Features.Role.UpdateRole;

public sealed record UpdateRoleRequest(string Name, List<string> Permissions);

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).NotEmpty().MaximumLength(100);
    }
}

public sealed record UpdateRoleCommand(Guid Id, UpdateRoleRequest Request) : IHasId;

public sealed class UpdateRoleCommandHandler
{
    public static async Task<ErrorOr<CustomRole>> LoadAsync(
        UpdateRoleCommand command,
        IRoleRepository repository,
        CancellationToken ct
    )
    {
        var role = await repository.FirstOrDefaultAsync(new RoleByIdSpecification(command.Id), ct);
        if (role == null)
            return Error.NotFound("Role.NotFound", "Role not found.");
        if (role.IsImmutable)
            return Error.Validation("Role.Immutable", "Cannot modify built-in roles.");
        return role;
    }

    public static async Task<(ErrorOr<RoleResponse>, OutgoingMessages)> HandleAsync(
        UpdateRoleCommand command,
        IRoleRepository repository,
        IUnitOfWork<IdentityDbMarker> unitOfWork,
        IHttpContextAccessor httpContextAccessor,
        ErrorOr<CustomRole> roleResult,
        CancellationToken ct
    )
    {
        if (roleResult.IsError)
            return (roleResult.Errors, OutgoingMessagesHelper.Empty);
        var role = roleResult.Value;

        var user = httpContextAccessor.HttpContext?.User;
        bool isPlatformAdmin =
            user?.HasClaim(AuthConstants.Claims.Permission, Permission.Platform.Manage) == true;

        if (!isPlatformAdmin && command.Request.Permissions.Contains(Permission.Platform.Manage))
        {
            return (
                Error.Forbidden(
                    "Role.Permissions",
                    "TenantAdmin cannot grant Platform.Manage permission."
                ),
                OutgoingMessagesHelper.Empty
            );
        }

        role.Name = command.Request.Name;
        role.Permissions.Clear();

        foreach (var perm in command.Request.Permissions)
        {
            role.Permissions.Add(
                new RolePermission
                {
                    RoleId = role.Id,
                    Permission = perm,
                    Role = role,
                }
            );
        }

        await repository.UpdateAsync(role, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new RolePermissionsInvalidatedEvent(role.Id));

        return (role.ToResponse(), messages);
    }
}
