using System.Security.Claims;
using Identity.Auth.Security.Keycloak;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APITemplate.Tests.Unit.Identity;

[Trait("Category", "Unit")]
public sealed class KeycloakClaimMapperTests
{
    private readonly Mock<ILogger> _loggerMock = new();

    [Fact]
    public void MapKeycloakClaims_ShouldMapUsername_WhenPreferredUsernameClaimExists()
    {
        // Arrange
        ClaimsIdentity identity = new();
        identity.AddClaim(new Claim("preferred_username", "testuser"));

        // Act
        KeycloakClaimMapper.MapKeycloakClaims(identity, _loggerMock.Object);

        // Assert
        Claim? nameClaim = identity.FindFirst(ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal("testuser", nameClaim.Value);
    }

    [Fact]
    public void MapKeycloakClaims_ShouldMapRoles_WhenValidRealmAccessExists()
    {
        // Arrange
        ClaimsIdentity identity = new();
        string realmAccessJson = "{\"roles\":[\"admin\", \"user\"]}";
        identity.AddClaim(new Claim("realm_access", realmAccessJson));

        // Act
        KeycloakClaimMapper.MapKeycloakClaims(identity, _loggerMock.Object);

        // Assert
        List<string> roles = identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("admin", roles);
        Assert.Contains("user", roles);
    }

    [Fact]
    public void MapKeycloakClaims_ShouldNotThrow_AndShouldLogWarning_WhenRealmAccessIsInvalidJson()
    {
        // Arrange
        ClaimsIdentity identity = new();
        string invalidJson = "{ invalid }";
        identity.AddClaim(new Claim("realm_access", invalidJson));

        // Act
        KeycloakClaimMapper.MapKeycloakClaims(identity, _loggerMock.Object);

        // Assert
        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        _loggerMock.Verify(
            x =>
                x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("Failed to parse Keycloak 'realm_access'")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void MapKeycloakClaims_ShouldHandleNonArrayRoles_Gracefully()
    {
        // Arrange
        ClaimsIdentity identity = new();
        string invalidRolesJson = "{\"roles\": \"not-an-array\"}";
        identity.AddClaim(new Claim("realm_access", invalidRolesJson));

        // Act
        KeycloakClaimMapper.MapKeycloakClaims(identity, _loggerMock.Object);

        // Assert
        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }
}
