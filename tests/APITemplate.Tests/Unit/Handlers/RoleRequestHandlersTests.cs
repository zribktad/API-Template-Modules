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
        SetupUserClaims(isPlatformAdmin: false);
    }

    private void SetupUserClaims(bool isPlatformAdmin)
    {
        List<Claim> claims = new();
        if (isPlatformAdmin)
            claims.Add(new Claim(AuthConstants.Claims.Permission, Permission.Platform.Manage));
        ClaimsIdentity identity = new(claims, "Test");
        ClaimsPrincipal principal = new(identity);
        DefaultHttpContext httpContext = new() { User = principal };
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    [Fact]
    public async Task CreateRole_Success_PersistsAndReturnsResponse()
    {
        CreateRoleRequest request = new("Test Role", new List<string> { "Test.Permission" });
        CreateRoleCommand command = new(request);

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
            r => r.AddAsync(
                It.Is<CustomRole>(role =>
                    role.TenantId == _tenantId &&
                    role.Name == "Test Role" &&
                    role.Permissions.Count == 1 &&
                    role.Permissions.Any(p => p.Permission == "Test.Permission")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRole_WithNoPermissions_PersistsRoleWithEmptyPermissions()
    {
        CreateRoleRequest request = new("Empty Role", new List<string>());
        CreateRoleCommand command = new(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages _) =
            await CreateRoleCommandHandler.HandleAsync(
                command,
                _repository.Object,
                _unitOfWork.Object,
                _tenantProvider.Object,
                _httpContextAccessor.Object,
                CancellationToken.None
            );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldBeEmpty();
        _repository.Verify(
            r => r.AddAsync(
                It.Is<CustomRole>(role => role.TenantId == _tenantId && role.Permissions.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRole_PlatformAdmin_CanGrantPlatformManage_Succeeds()
    {
        SetupUserClaims(isPlatformAdmin: true);
        CreateRoleRequest request = new("Admin Role", new List<string> { Permission.Platform.Manage });
        CreateRoleCommand command = new(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages _) =
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
        _repository.Verify(
            r => r.AddAsync(
                It.Is<CustomRole>(role => role.Permissions.Any(p => p.Permission == Permission.Platform.Manage)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRole_TenantAdmin_WithPlatformManageAmongOthers_ReturnsError()
    {
        CreateRoleRequest request = new("Mixed Role", new List<string> { "Reports.Read", Permission.Platform.Manage });
        CreateRoleCommand command = new(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages _) =
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
        _repository.Verify(r => r.AddAsync(It.IsAny<CustomRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateRole_Success_RoleIsNotImmutable()
    {
        CreateRoleRequest request = new("Custom Role", new List<string> { "Reports.Read" });
        CreateRoleCommand command = new(request);

        (ErrorOr<RoleResponse> result, OutgoingMessages _) =
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
            r => r.AddAsync(
                It.Is<CustomRole>(role => !role.IsImmutable),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRole_TenantAdmin_CannotGrantPlatformManage_ReturnsError()
    {
        CreateRoleRequest request = new("Test Role", new List<string> { Permission.Platform.Manage });
        CreateRoleCommand command = new(request);

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
        _repository.Verify(r => r.AddAsync(It.IsAny<CustomRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRole_Immutable_ReturnsError()
    {
        Guid roleId = Guid.NewGuid();
        UpdateRoleRequest request = new("Updated Name", new List<string>());
        UpdateRoleCommand command = new(roleId, request);

        CustomRole immutableRole = new()
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
        Guid roleId = Guid.NewGuid();
        UpdateRoleRequest request = new(
            "Updated Name",
            new List<string> { Permission.Platform.Manage }
        );
        UpdateRoleCommand command = new(roleId, request);
        CustomRole role = new()
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
        Guid roleId = Guid.NewGuid();
        UpdateRoleRequest request = new(
            "Updated Name",
            new List<string> { Permission.Platform.Manage }
        );
        UpdateRoleCommand command = new(roleId, request);
        CustomRole role = new()
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
        Guid roleId = Guid.NewGuid();
        UpdateRoleRequest request = new("Updated Name", new List<string> { "New.Perm" });
        UpdateRoleCommand command = new(roleId, request);
        CustomRole role = new()
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
            .OfType<Identity.Directory.Features.Role.InvalidatePermissions.RolePermissionsInvalidatedEvent>()
            .ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeleteRole_Immutable_ReturnsError()
    {
        Guid roleId = Guid.NewGuid();
        DeleteRoleCommand command = new(roleId);

        CustomRole immutableRole = new()
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
        Guid roleId = Guid.NewGuid();
        DeleteRoleCommand command = new(roleId);
        CustomRole role = new()
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
            .OfType<Identity.Directory.Features.Role.InvalidatePermissions.RolePermissionsInvalidatedEvent>()
            .ShouldHaveSingleItem();
    }
}
