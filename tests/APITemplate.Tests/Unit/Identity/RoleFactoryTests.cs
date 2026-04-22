using ErrorOr;
using Identity.Directory.Entities;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

public class CustomRoleCreateTests
{
    [Fact]
    public void Create_TenantAdmin_CannotGrantPlatformManage_ReturnsError()
    {
        ErrorOr<CustomRole> result = CustomRole.Create(
            Guid.NewGuid(), "Test", Guid.NewGuid(),
            [Permission.Platform.Manage], isPlatformAdmin: false
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
            id, "Admin Role", null,
            [Permission.Platform.Manage], isPlatformAdmin: true
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
            Guid.NewGuid(), "Custom Role", tenantId,
            ["Reports.Read"], isPlatformAdmin: false
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
            Guid.NewGuid(), "Empty Role", Guid.NewGuid(),
            [], isPlatformAdmin: false
        );

        result.IsError.ShouldBeFalse();
        result.Value.Permissions.ShouldBeEmpty();
    }
}
