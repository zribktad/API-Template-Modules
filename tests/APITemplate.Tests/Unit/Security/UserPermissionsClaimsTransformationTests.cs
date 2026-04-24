using System.Security.Claims;
using System.Text.Json;
using Identity.Auth.Security;
using Identity.Directory.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SharedKernel.Contracts.Security;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Security;

[Trait("Category", "Unit")]
public class UserPermissionsClaimsTransformationTests
{
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly Mock<IServiceProvider> _serviceProvider = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IServiceScope> _scope = new();
    private readonly Mock<IServiceProvider> _scopedProvider = new();
    private readonly Mock<IUserRepository> _userRepository = new();

    public UserPermissionsClaimsTransformationTests()
    {
        _serviceProvider
            .Setup(s => s.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactory.Object);
        _scopeFactory.Setup(s => s.CreateScope()).Returns(_scope.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_scopedProvider.Object);

        _scopedProvider
            .Setup(s => s.GetService(typeof(IUserRepository)))
            .Returns(_userRepository.Object);

        _userRepository
            .Setup(r =>
                r.ListDistinctPermissionNamesBySubjectAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
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
        identity.AddClaim(new Claim(AuthConstants.Claims.Permission, "Some.Perm"));
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
        result
            .HasClaim(AuthConstants.Claims.Permission, Permission.Platform.Manage)
            .ShouldBeTrue();
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
            .Setup(c =>
                c.GetAsync(
                    AuthConstants.DistributedCache.UserPermissionsCacheKey(subject),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(cachedBytes);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var extractedPermissions = result
            .FindAll(AuthConstants.Claims.Permission)
            .Select(c => c.Value)
            .ToList();
        extractedPermissions.Count.ShouldBe(2);
        extractedPermissions.ShouldContain("Test.Perm1");
        extractedPermissions.ShouldContain("Test.Perm2");

        _userRepository.Verify(
            r =>
                r.ListDistinctPermissionNamesBySubjectAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task TransformAsync_WhenCacheContainsInvalidJson_RemovesKeyAndLoadsFromRepository()
    {
        var userId = Guid.NewGuid();
        var transformation = new UserPermissionsClaimsTransformation(
            _serviceProvider.Object,
            _cache.Object
        );
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(AuthConstants.Claims.Subject, userId.ToString()));
        var principal = new ClaimsPrincipal(identity);

        var badJson = System.Text.Encoding.UTF8.GetBytes("{not-json");
        _cache
            .Setup(c =>
                c.GetAsync(
                    AuthConstants.DistributedCache.UserPermissionsCacheKey(userId.ToString()),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(badJson);

        _cache
            .Setup(c =>
                c.RemoveAsync(
                    AuthConstants.DistributedCache.UserPermissionsCacheKey(userId.ToString()),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        _cache
            .Setup(c =>
                c.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var result = await transformation.TransformAsync(principal);

        _cache.Verify(
            c =>
                c.RemoveAsync(
                    AuthConstants.DistributedCache.UserPermissionsCacheKey(userId.ToString()),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _userRepository.Verify(
            r =>
                r.ListDistinctPermissionNamesBySubjectAsync(
                    userId.ToString(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        result.ShouldNotBeNull();
    }
}
