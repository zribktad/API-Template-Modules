using ErrorOr;
using Wolverine;

namespace Identity.Directory.Features.User.AssignRoles;

public sealed record AssignUserRolesRequest(List<Guid> RoleIds);

public sealed record AssignUserRolesCommand(Guid UserId, AssignUserRolesRequest Request) : IHasId
{
    public Guid Id => UserId;
}
