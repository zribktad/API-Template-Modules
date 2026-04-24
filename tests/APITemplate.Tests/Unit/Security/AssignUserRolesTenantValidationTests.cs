using ErrorOr;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.User.AssignRoles;
using Identity.Directory.Interfaces;
using Identity.Errors;
using Identity.ValueObjects;
using Moq;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

[Trait("Category", "Unit")]
public class AssignUserRolesTenantValidationTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IUnitOfWork<global::Identity.IdentityDbMarker>> _unitOfWork = new();

    [Fact]
    public async Task AssignRoles_WhenRoleBelongsToAnotherTenant_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var command = new AssignUserRolesCommand(
            userId,
            new AssignUserRolesRequest(new List<Guid> { roleId })
        );

        var user = new AppUser
        {
            Id = userId,
            TenantId = tenantId,
            Username = new NormalizedString("test"),
            Email = new NormalizedString("test@test.com"),
        };

        var foreignRole = new CustomRole
        {
            Id = roleId,
            Name = "Other",
            TenantId = otherTenantId,
        };

        _roleRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<RolesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<CustomRole> { foreignRole });

        (ErrorOr<Success> result, OutgoingMessages _) =
            await AssignUserRolesCommandHandler.HandleAsync(
                command,
                _userRepository.Object,
                _roleRepository.Object,
                _unitOfWork.Object,
                user,
                CancellationToken.None
            );

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(ErrorCatalog.Roles.CannotAssignForeignTenant);
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
    }
}
