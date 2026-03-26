using System.Security.Claims;
using APITemplate.Api.Authorization;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

public class PermissionAuthorizationHandlerTests
{
    private readonly IRolePermissionMap _rolePermissionMap = new StaticRolePermissionMap();
    private readonly PermissionAuthorizationHandler _handler;

    public PermissionAuthorizationHandlerTests()
    {
        _handler = new PermissionAuthorizationHandler(_rolePermissionMap);
    }

    [Fact]
    public async Task UserWithCorrectRole_Succeeds()
    {
        var requirement = new PermissionRequirement(Permission.Products.Read);
        var user = CreatePrincipal(UserRole.User);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UserWithoutPermission_Fails()
    {
        var requirement = new PermissionRequirement(Permission.Products.Create);
        var user = CreatePrincipal(UserRole.User);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task PlatformAdmin_SucceedsForAnyPermission()
    {
        var requirement = new PermissionRequirement(Permission.Users.Delete);
        var user = CreatePrincipal(UserRole.PlatformAdmin);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantAdmin_SucceedsForProductCreate()
    {
        var requirement = new PermissionRequirement(Permission.Products.Create);
        var user = CreatePrincipal(UserRole.TenantAdmin);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantAdmin_FailsForUserCreate()
    {
        var requirement = new PermissionRequirement(Permission.Users.Create);
        var user = CreatePrincipal(UserRole.TenantAdmin);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task UnauthenticatedUser_Fails()
    {
        var requirement = new PermissionRequirement(Permission.Products.Read);
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(UserRole role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
