using System.Security.Claims;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Directory.Features.Role.GetPermissions;
using Identity.Directory.Features.Role.GetRoles;
using Identity.Directory.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class RoleQueriesTests
{
    [Fact]
    public async Task GetRolesQueryHandler_ReturnsMappedRoles()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(x => x.TenantId).Returns(tenantId);

        var repository = new Mock<IRoleRepository>();
        var roles = new List<CustomRole>
        {
            new CustomRole
            {
                Id = Guid.NewGuid(),
                Name = "Role 1",
                TenantId = tenantId,
            },
            new CustomRole
            {
                Id = Guid.NewGuid(),
                Name = "Role 2",
                TenantId = tenantId,
            },
        };
        repository
            .Setup(x =>
                x.ListAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<CustomRole>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(roles);

        // Act
        var result = await GetRolesQueryHandler.HandleAsync(
            new GetRolesQuery(),
            repository.Object,
            tenantProvider.Object,
            CancellationToken.None
        );

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Name.ShouldBe("Role 1");
    }

    [Fact]
    public async Task GetPermissionsQueryHandler_PlatformAdmin_ReturnsAllPermissions()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new Claim(AuthConstants.Claims.Permission, Permission.Platform.Manage),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        httpContextAccessor
            .Setup(x => x.HttpContext)
            .Returns(new DefaultHttpContext { User = principal });

        // Act
        var result = await GetPermissionsQueryHandler.HandleAsync(
            new GetPermissionsQuery(),
            httpContextAccessor.Object,
            CancellationToken.None
        );

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldContain(Permission.Platform.Manage);
        result.Value.Count.ShouldBe(Permission.All.Count);
    }

    [Fact]
    public async Task GetPermissionsQueryHandler_TenantAdmin_FiltersPlatformManage()
    {
        // Arrange
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>(), "Test"));
        httpContextAccessor
            .Setup(x => x.HttpContext)
            .Returns(new DefaultHttpContext { User = principal });

        // Act
        var result = await GetPermissionsQueryHandler.HandleAsync(
            new GetPermissionsQuery(),
            httpContextAccessor.Object,
            CancellationToken.None
        );

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldNotContain(Permission.Platform.Manage);
        result.Value.Count.ShouldBeLessThan(Permission.All.Count);
    }
}
