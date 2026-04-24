using ErrorOr;
using Identity.Directory.Entities;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public class CustomRoleCreateTests
{
    [Fact]
    public void Create_TenantAdmin_CannotGrantPlatformManage_ReturnsError()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Test",
            Guid.NewGuid(),
            [Permission.Platform.Manage],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
        result.FirstError.Code.ShouldBe("ROL-0403-PLATFORM");
    }

    [Fact]
    public void Create_PlatformAdmin_CanGrantPlatformManage_Succeeds()
    {
        Guid id = Guid.NewGuid();
        ErrorOr<CustomRole> result = CustomRole.Create(
            id,
            "Admin Role",
            null,
            [Permission.Platform.Manage],
            isPlatformAdmin: true
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(id);
        result.Value.Permissions.ShouldContain(p => p.Permission == Permission.Platform.Manage);
    }

    [Fact]
    public void Create_TenantAdmin_RegularPermissions_Succeeds()
    {
        Guid tenantId = Guid.NewGuid();
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Custom Role",
            tenantId,
            ["Reports.Read"],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Custom Role");
        result.Value.TenantId.ShouldBe(tenantId);
        result.Value.IsImmutable.ShouldBeFalse();
        result.Value.Permissions.ShouldContain(p => p.Permission == "Reports.Read");
    }

    [Fact]
    public void Create_EmptyPermissions_Succeeds()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Empty Role",
            Guid.NewGuid(),
            [],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Create_NullPermissions_TreatedAsEmpty()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Null Perms Role",
            Guid.NewGuid(),
            null!,
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Create_PreservesProvidedId()
    {
        Guid expectedId = Guid.NewGuid();

        ErrorOr<CustomRole> result = CustomRole.Create(
            expectedId,
            "Role",
            Guid.NewGuid(),
            ["Some.Permission"],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(expectedId);
    }

    [Fact]
    public void Create_GlobalRole_NullTenantId_Succeeds()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Global Role",
            tenantId: null,
            ["Reports.Read"],
            isPlatformAdmin: true
        );

        result.IsError.ShouldBeFalse();
        result.Value.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_MultiplePermissions_AllPersisted()
    {
        string[] permissions = ["Reports.Read", "Reports.Write", "Users.Read"];

        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Multi Role",
            Guid.NewGuid(),
            permissions,
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.Count.ShouldBe(3);
        result.Value.Permissions.ShouldContain(p => p.Permission == "Reports.Read");
        result.Value.Permissions.ShouldContain(p => p.Permission == "Reports.Write");
        result.Value.Permissions.ShouldContain(p => p.Permission == "Users.Read");
    }

    [Fact]
    public void Create_TenantAdmin_PlatformManageAmongOtherPermissions_ReturnsError()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Mixed Role",
            Guid.NewGuid(),
            ["Reports.Read", Permission.Platform.Manage, "Users.Read"],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void Create_PlatformAdmin_MultiplePermissionsIncludingManage_AllPersisted()
    {
        string[] permissions = ["Reports.Read", Permission.Platform.Manage];

        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Super Role",
            tenantId: null,
            permissions,
            isPlatformAdmin: true
        );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.Count.ShouldBe(2);
        result.Value.Permissions.ShouldContain(p => p.Permission == Permission.Platform.Manage);
    }

    [Fact]
    public void Create_NewRole_IsNotImmutable()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(),
            "Any Role",
            Guid.NewGuid(),
            ["Some.Permission"],
            isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.IsImmutable.ShouldBeFalse();
    }
}
