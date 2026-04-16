using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.InvalidatePermissions;
using Identity.Directory.Features.Role.Shared;
using Microsoft.AspNetCore.Http;
using Identity.Directory.Interfaces;
using SharedKernel.Application.Validation;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
using System.ComponentModel.DataAnnotations;
using Wolverine;

namespace Identity.Directory.Features.Role.UpdateRole;

public sealed record UpdateRoleRequest(
    [NotEmpty] [MaxLength(100)] string Name,
    [Required]
    [NoWhitespaceItems]
    [MaxLengthItems(100)]
        List<string> Permissions
);

public sealed record UpdateRoleCommand(Guid Id, UpdateRoleRequest Request) : IHasId;

public sealed class UpdateRoleCommandHandler
{
    public static Task<ErrorOr<CustomRole>> LoadAsync(
        UpdateRoleCommand command,
        IRoleRepository repository,
        CancellationToken ct
    ) => RoleLoader.LoadMutableAsync(command.Id, repository, ct);

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
        CustomRole role = roleResult.Value;

        bool isPlatformAdmin = httpContextAccessor.HttpContext?.User.IsPlatformAdmin() == true;

        if (!isPlatformAdmin && command.Request.Permissions.Contains(Permission.Platform.Manage))
            return (DomainErrors.Roles.CannotGrantPlatformManage(), OutgoingMessagesHelper.Empty);

        role.Name = command.Request.Name;
        role.SetPermissions(command.Request.Permissions);

        await repository.UpdateAsync(role, ct);
        await unitOfWork.CommitAsync(ct);

        OutgoingMessages messages = new();
        messages.Add(new RolePermissionsInvalidatedEvent(role.Id));

        return (role.ToResponse(), messages);
    }
}
