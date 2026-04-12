using System.Security.Claims;
using APITemplate.Api.Authorization;
using Identity.Auth.Security;
using Identity.Directory.Enums;
using Microsoft.AspNetCore.Authorization;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _handler;
    private readonly IRolePermissionMap _rolePermissionMap = new StaticRolePermissionMap();

    public PermissionAuthorizationHandlerTests()
    {
        _handler = new PermissionAuthorizationHandler(_rolePermissionMap);
    }

    [Fact]
    public async Task UserWithCorrectRole_Succeeds()
    {
        PermissionRequirement requirement = new(Permission.Products.Read);
        ClaimsPrincipal user = CreatePrincipal(UserRole.User);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UserWithoutPermission_Fails()
    {
        PermissionRequirement requirement = new(Permission.Products.Create);
        ClaimsPrincipal user = CreatePrincipal(UserRole.User);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task PlatformAdmin_SucceedsForAnyPermission()
    {
        PermissionRequirement requirement = new(Permission.Users.Delete);
        ClaimsPrincipal user = CreatePrincipal(UserRole.PlatformAdmin);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantAdmin_SucceedsForProductCreate()
    {
        PermissionRequirement requirement = new(Permission.Products.Create);
        ClaimsPrincipal user = CreatePrincipal(UserRole.TenantAdmin);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TenantAdmin_FailsForUserCreate()
    {
        PermissionRequirement requirement = new(Permission.Users.Create);
        ClaimsPrincipal user = CreatePrincipal(UserRole.TenantAdmin);
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task UnauthenticatedUser_Fails()
    {
        PermissionRequirement requirement = new(Permission.Products.Read);
        ClaimsPrincipal user = new(new ClaimsIdentity());
        AuthorizationHandlerContext context = new([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(UserRole role)
    {
        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
