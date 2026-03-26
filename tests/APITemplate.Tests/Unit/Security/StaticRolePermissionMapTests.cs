using APITemplate.Application.Common.Security;
using APITemplate.Domain.Enums;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

public class StaticRolePermissionMapTests
{
    private readonly StaticRolePermissionMap _map = new();

    [Fact]
    public void PlatformAdmin_HasAllPermissions()
    {
        var permissions = _map.GetPermissions(UserRole.PlatformAdmin.ToString());

        permissions.Count.ShouldBe(Permission.All.Count);
        foreach (var permission in Permission.All)
        {
            permissions.ShouldContain(permission);
        }
    }

    [Fact]
    public void TenantAdmin_HasExpectedPermissions()
    {
        var role = UserRole.TenantAdmin.ToString();

        _map.HasPermission(role, Permission.Products.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.Products.Create).ShouldBeTrue();
        _map.HasPermission(role, Permission.Products.Update).ShouldBeTrue();
        _map.HasPermission(role, Permission.Products.Delete).ShouldBeTrue();
        _map.HasPermission(role, Permission.Categories.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.Categories.Create).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductReviews.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductReviews.Create).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductReviews.Delete).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductData.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductData.Create).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductData.Delete).ShouldBeTrue();
        _map.HasPermission(role, Permission.Users.Read).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Permission.Users.Create)]
    [InlineData(Permission.Users.Update)]
    [InlineData(Permission.Users.Delete)]
    public void TenantAdmin_DoesNotHaveUserWritePermissions(string permission)
    {
        _map.HasPermission(UserRole.TenantAdmin.ToString(), permission).ShouldBeFalse();
    }

    [Fact]
    public void User_HasReadOnlyAndReviewCreate()
    {
        var role = UserRole.User.ToString();

        _map.HasPermission(role, Permission.Products.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.Categories.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductReviews.Read).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductReviews.Create).ShouldBeTrue();
        _map.HasPermission(role, Permission.ProductData.Read).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Permission.Products.Create)]
    [InlineData(Permission.Products.Update)]
    [InlineData(Permission.Products.Delete)]
    [InlineData(Permission.Categories.Create)]
    [InlineData(Permission.Users.Read)]
    public void User_DoesNotHaveMutationPermissions(string permission)
    {
        _map.HasPermission(UserRole.User.ToString(), permission).ShouldBeFalse();
    }

    [Fact]
    public void UnknownRole_ReturnsEmptyPermissions()
    {
        var permissions = _map.GetPermissions("UnknownRole");

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void PermissionAll_ContainsExpectedCount()
    {
        // 4 Products + 4 Categories + 3 ProductReviews + 3 ProductData + 4 Users + 3 Tenants + 3 Invitations + 6 Examples = 30
        Permission.All.Count.ShouldBe(30);
    }
}
