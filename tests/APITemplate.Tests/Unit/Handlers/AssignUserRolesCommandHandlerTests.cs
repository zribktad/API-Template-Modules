using ErrorOr;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.User.AssignRoles;
using Identity.Directory.Interfaces;
using Identity.ValueObjects;
using Moq;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class AssignUserRolesCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IUnitOfWork<Identity.Persistence.IdentityDbMarker>> _unitOfWork = new();

    [Fact]
    public async Task AssignRoles_WhenSomeRolesDoNotExist_ReturnsValidationError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role1 = Guid.NewGuid();
        var role2 = Guid.NewGuid();
        var command = new AssignUserRolesCommand(
            userId,
            new AssignUserRolesRequest(new List<Guid> { role1, role2 })
        );

        var user = new AppUser
        {
            Id = userId,
            Username = new NormalizedString("test"),
            Email = new NormalizedString("test@test.com"),
        };

        // Return only one role, simulating one missing
        _roleRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<RolesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new List<CustomRole>
                {
                    new CustomRole { Id = role1, Name = "Role1" },
                }
            );

        // Act
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await AssignUserRolesCommandHandler.HandleAsync(
                command,
                _userRepository.Object,
                _roleRepository.Object,
                _unitOfWork.Object,
                user,
                CancellationToken.None
            );

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Roles.Invalid");
    }

    [Fact]
    public async Task AssignRoles_Success()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role1 = Guid.NewGuid();
        var command = new AssignUserRolesCommand(
            userId,
            new AssignUserRolesRequest(new List<Guid> { role1 })
        );

        var user = new AppUser
        {
            Id = userId,
            Username = new NormalizedString("test"),
            Email = new NormalizedString("test@test.com"),
        };
        var role = new CustomRole { Id = role1, Name = "Role1" };

        _roleRepository
            .Setup(r =>
                r.ListAsync(It.IsAny<RolesByIdsSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new List<CustomRole> { role });

        // Act
        (ErrorOr<Success> result, OutgoingMessages messages) =
            await AssignUserRolesCommandHandler.HandleAsync(
                command,
                _userRepository.Object,
                _roleRepository.Object,
                _unitOfWork.Object,
                user,
                CancellationToken.None
            );

        // Assert
        result.IsError.ShouldBeFalse();
        user.Roles.ShouldContain(role);

        _userRepository.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        messages.OfType<UserPermissionsInvalidatedEvent>().ShouldHaveSingleItem();
    }
}
