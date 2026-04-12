using System.Security.Claims;
using ErrorOr;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.CreateRole;
using Identity.Directory.Features.Role.DeleteRole;
using Identity.Directory.Features.Role.Shared;
using Identity.Directory.Features.Role.UpdateRole;
using Identity.Directory.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Wolverine;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class RoleRequestHandlersTests
{
    private readonly Mock<IRoleRepository> _repository = new();
    private readonly Mock<IUnitOfWork<Identity.Persistence.IdentityDbMarker>> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    public RoleRequestHandlersTests()
    {
        _tenantProvider.Setup(x => x.TenantId).Returns(_tenantId);
        SetupUserClaims(isPlatformAdmin: false); // Default to TenantAdmin
    }

    private void SetupUserClaims(bool isPlatformAdmin)
    {
        var claims = new List<Claim>();
        if (isPlatformAdmin)
        {
            claims.Add(new Claim(AuthConstants.Claims.Permission, Permission.Platform.Manage));
        }
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    [Fact]
    public async Task CreateRole_Success()
    {
        var request = new CreateRoleRequest("Test Role", new List<string> { "Test.Permission" });
        var command = new CreateRoleCommand(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Test Role");
        result.Value.Permissions.ShouldContain("Test.Permission");

        _repository.Verify(
            r =>
                r.AddAsync(
                    It.Is<CustomRole>(c => c.Name == "Test Role" && c.TenantId == _tenantId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRole_TenantAdmin_CannotGrantPlatformManage()
    {
        SetupUserClaims(isPlatformAdmin: false);
        var request = new CreateRoleRequest(
            "Test Role",
            new List<string> { Permission.Platform.Manage }
        );
        var command = new CreateRoleCommand(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
        result.FirstError.Code.ShouldBe("Role.Permissions");
    }

    [Fact]
    public async Task CreateRole_PlatformAdmin_CanGrantPlatformManage()
    {
        SetupUserClaims(isPlatformAdmin: true);
        var request = new CreateRoleRequest(
            "Test Role",
            new List<string> { Permission.Platform.Manage }
        );
        var command = new CreateRoleCommand(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldContain(Permission.Platform.Manage);
    }

    [Fact]
    public async Task CreateRole_PlatformAdmin_CanSpecifyTenantId()
    {
        SetupUserClaims(isPlatformAdmin: true);
        var explicitTenantId = Guid.NewGuid();
        var request = new CreateRoleRequest("Test Role", new List<string>(), explicitTenantId);
        var command = new CreateRoleCommand(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        _repository.Verify(
            r =>
                r.AddAsync(
                    It.Is<CustomRole>(c => c.TenantId == explicitTenantId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateRole_TenantAdmin_TenantIdIsIgnored()
    {
        SetupUserClaims(isPlatformAdmin: false);
        var explicitTenantId = Guid.NewGuid(); // Some other tenant
        var request = new CreateRoleRequest("Test Role", new List<string>(), explicitTenantId);
        var command = new CreateRoleCommand(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        // Should ignore explicitTenantId and use _tenantId
        _repository.Verify(
            r =>
                r.AddAsync(
                    It.Is<CustomRole>(c => c.TenantId == _tenantId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateRole_Immutable_ReturnsError()
    {
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("Updated Name", new List<string>());
        var command = new UpdateRoleCommand(roleId, request);

        var immutableRole = new CustomRole
        {
            Id = roleId,
            Name = "PlatformAdmin",
            IsImmutable = true,
        };
        _repository
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<RoleByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(immutableRole);

        ErrorOr<CustomRole> loadResult = await UpdateRoleCommandHandler.LoadAsync(
            command,
            _repository.Object,
            CancellationToken.None
        );

        loadResult.IsError.ShouldBeTrue();
        loadResult.FirstError.Code.ShouldBe("Role.Immutable");
    }

    [Fact]
    public async Task UpdateRole_TenantAdmin_CannotGrantPlatformManage()
    {
        SetupUserClaims(isPlatformAdmin: false);
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest(
            "Updated Name",
            new List<string> { Permission.Platform.Manage }
        );
        var command = new UpdateRoleCommand(roleId, request);
        var role = new CustomRole
        {
            Id = roleId,
            Name = "Old Name",
            IsImmutable = false,
        };

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await UpdateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _httpContextAccessor.Object,
                role,
                CancellationToken.None
            );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task UpdateRole_PlatformAdmin_CanGrantPlatformManage()
    {
        SetupUserClaims(isPlatformAdmin: true);
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest(
            "Updated Name",
            new List<string> { Permission.Platform.Manage }
        );
        var command = new UpdateRoleCommand(roleId, request);
        var role = new CustomRole
        {
            Id = roleId,
            Name = "Old Name",
            IsImmutable = false,
        };

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await UpdateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _httpContextAccessor.Object,
                role,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldContain(Permission.Platform.Manage);
    }

    [Fact]
    public async Task UpdateRole_Success()
    {
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest("Updated Name", new List<string> { "New.Perm" });
        var command = new UpdateRoleCommand(roleId, request);
        var role = new CustomRole
        {
            Id = roleId,
            Name = "Old Name",
            IsImmutable = false,
        };

        (ErrorOr<RoleResponse> result, OutgoingMessages messages) =
            await UpdateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _httpContextAccessor.Object,
                role,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Updated Name");
        result.Value.Permissions.ShouldContain("New.Perm");

        _repository.Verify(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        messages
            .OfType<Identity.Directory.Features.User.InvalidatePermissions.RolePermissionsInvalidatedEvent>()
            .ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeleteRole_Immutable_ReturnsError()
    {
        var roleId = Guid.NewGuid();
        var command = new DeleteRoleCommand(roleId);

        var immutableRole = new CustomRole
        {
            Id = roleId,
            Name = "PlatformAdmin",
            IsImmutable = true,
        };
        _repository
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<RoleByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(immutableRole);

        ErrorOr<CustomRole> loadResult = await DeleteRoleCommandHandler.LoadAsync(
            command,
            _repository.Object,
            CancellationToken.None
        );

        loadResult.IsError.ShouldBeTrue();
        loadResult.FirstError.Code.ShouldBe("Role.Immutable");
    }

    [Fact]
    public async Task DeleteRole_Success()
    {
        var roleId = Guid.NewGuid();
        var command = new DeleteRoleCommand(roleId);
        var role = new CustomRole
        {
            Id = roleId,
            Name = "To Delete",
            IsImmutable = false,
        };

        (ErrorOr<Success> result, OutgoingMessages messages) =
            await DeleteRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                role,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();

        _repository.Verify(r => r.DeleteAsync(role, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        messages
            .OfType<Identity.Directory.Features.User.InvalidatePermissions.RolePermissionsInvalidatedEvent>()
            .ShouldHaveSingleItem();
    }
}
