using System.Security.Claims;
using System.Text.Json;
using Identity.Auth.Security;
using Identity.Directory.Entities;
using Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SharedKernel.Application.Context;
using SharedKernel.Contracts.Security;
using SharedKernel.Domain.Interfaces;
using SharedKernel.Infrastructure.Auditing;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

public class UserPermissionsClaimsTransformationTests
{
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly Mock<IServiceProvider> _serviceProvider = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IServiceScope> _scope = new();
    private readonly Mock<IServiceProvider> _scopedProvider = new();

    public UserPermissionsClaimsTransformationTests()
    {
        _serviceProvider
            .Setup(s => s.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactory.Object);
        _scopeFactory.Setup(s => s.CreateScope()).Returns(_scope.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_scopedProvider.Object);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new IdentityDbContext(
            options,
            Mock.Of<ITenantProvider>(),
            Mock.Of<IActorProvider>(),
            TimeProvider.System,
            Mock.Of<IAuditableEntityStateManager>()
        );
        _scopedProvider.Setup(s => s.GetService(typeof(IdentityDbContext))).Returns(dbContext);
    }

    [Fact]
    public async Task TransformAsync_WhenUnauthenticated_ReturnsSamePrincipal()
    {
        // Arrange
        var transformation = new UserPermissionsClaimsTransformation(
            _serviceProvider.Object,
            _cache.Object
        );
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        result.ShouldBe(principal);
        result.Claims.ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WhenAlreadyHasPermission_ReturnsSamePrincipal()
    {
        // Arrange
        var transformation = new UserPermissionsClaimsTransformation(
            _serviceProvider.Object,
            _cache.Object
        );
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("Permission", "Some.Perm"));
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        result.ShouldBe(principal);
        result.Claims.Count().ShouldBe(1);
    }

    [Fact]
    public async Task TransformAsync_WhenServiceAccount_AddsPermissionsBasedOnRole()
    {
        // Arrange
        var transformation = new UserPermissionsClaimsTransformation(
            _serviceProvider.Object,
            _cache.Object
        );
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(AuthConstants.Claims.Subject, "service-account-test"));
        identity.AddClaim(
            new Claim(AuthConstants.Claims.PreferredUsername, "service-account-test")
        );
        identity.AddClaim(new Claim(ClaimTypes.Role, "PlatformAdmin"));
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        result.HasClaim("Permission", Permission.Platform.Manage).ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithCacheHit_AddsPermissionsFromCache()
    {
        // Arrange
        var transformation = new UserPermissionsClaimsTransformation(
            _serviceProvider.Object,
            _cache.Object
        );
        var subject = Guid.NewGuid().ToString();
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(AuthConstants.Claims.Subject, subject));
        var principal = new ClaimsPrincipal(identity);

        var cachedPermissions = new List<string> { "Test.Perm1", "Test.Perm2" };
        var cachedBytes = System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(cachedPermissions)
        );

        _cache
            .Setup(c => c.GetAsync($"UserPermissions:{subject}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var extractedPermissions = result.FindAll("Permission").Select(c => c.Value).ToList();
        extractedPermissions.Count.ShouldBe(2);
        extractedPermissions.ShouldContain("Test.Perm1");
        extractedPermissions.ShouldContain("Test.Perm2");
    }
}
