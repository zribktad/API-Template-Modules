using System.Security.Claims;
using APITemplate.Api.Authorization;
using Identity.Auth.Security;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Api;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _handler = new();

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasPermissionClaim_Succeeds()
    {
        const string perm = "Test.Permission";
        var identity = new ClaimsIdentity("t");
        identity.AddClaim(new Claim(AuthConstants.Claims.Permission, perm));
        var user = new ClaimsPrincipal(identity);
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(perm)],
            user,
            resource: null
        );

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserLacksPermission_DoesNotSucceed()
    {
        var identity = new ClaimsIdentity("t");
        identity.AddClaim(new Claim(AuthConstants.Claims.Permission, "Other.Perm"));
        var user = new ClaimsPrincipal(identity);
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("Required.Perm")],
            user,
            resource: null
        );

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUnauthenticated_DoesNotSucceed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("Any.Perm")],
            user,
            resource: null
        );

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
