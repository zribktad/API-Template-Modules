using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Webhooks.Contracts;
using Webhooks.Security;
using Xunit;

namespace APITemplate.Tests.Unit.Webhooks;

[Trait("Category", "Unit")]
public sealed class OutgoingWebhookSecurityTests
{
    private readonly Mock<IHostEnvironment> _envMock = new();

    private DefaultNetworkSecurityPolicy CreatePolicy(
        bool allowLocal,
        string environment = "Production"
    )
    {
        _envMock.Setup(e => e.EnvironmentName).Returns(environment);
        IOptions<WebhookOptions> options = Options.Create(
            new WebhookOptions { AllowLocalRequests = allowLocal }
        );
        return new DefaultNetworkSecurityPolicy(_envMock.Object, options);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("100.64.0.1")]
    [InlineData("::ffff:10.0.0.1")] // IPv4-mapped IPv6 bypass
    public void IsAllowed_ShouldReturnFalse_ForRestrictedAddresses_InProduction(string ipAddress)
    {
        // Arrange
        DefaultNetworkSecurityPolicy policy = CreatePolicy(
            allowLocal: true,
            environment: "Production"
        ); // allowLocal is true but env is Prod
        IPAddress address = IPAddress.Parse(ipAddress);

        // Act
        bool result = policy.IsAllowed(address);

        // Assert
        Assert.False(result, $"Address {ipAddress} should be blocked in Production.");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    public void IsAllowed_ShouldReturnTrue_ForLocalAddresses_InDevelopment_WhenEnabled(
        string ipAddress
    )
    {
        // Arrange
        DefaultNetworkSecurityPolicy policy = CreatePolicy(
            allowLocal: true,
            environment: "Development"
        );
        IPAddress address = IPAddress.Parse(ipAddress);

        // Act
        bool result = policy.IsAllowed(address);

        // Assert
        Assert.True(
            result,
            $"Address {ipAddress} should be allowed in Development when AllowLocalRequests is true."
        );
    }

    [Fact]
    public void IsAllowed_ShouldReturnFalse_ForLocalAddresses_InDevelopment_WhenDisabled()
    {
        // Arrange
        DefaultNetworkSecurityPolicy policy = CreatePolicy(
            allowLocal: false,
            environment: "Development"
        );
        IPAddress address = IPAddress.Parse("127.0.0.1");

        // Act
        bool result = policy.IsAllowed(address);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("8.8.8.8", true)] // Public
    [InlineData("10.0.0.1", false)] // Private 10.x
    [InlineData("9.255.255.255", true)] // Just before 10.x
    [InlineData("11.0.0.0", true)] // Just after 10.x
    [InlineData("172.15.255.255", true)] // Just before 172.16.x
    [InlineData("172.16.0.0", false)] // Start of 172.16.x
    [InlineData("172.31.255.255", false)] // End of 172.16.x
    [InlineData("172.32.0.0", true)] // Just after 172.16.x
    [InlineData("192.167.255.255", true)] // Just before 192.168.x
    [InlineData("192.168.0.0", false)] // Start of 192.168.x
    [InlineData("192.169.0.0", true)] // Just after 192.168.x
    [InlineData("100.63.255.255", true)] // Just before CGNAT
    [InlineData("100.64.0.0", false)] // Start of CGNAT (100.64.0.0/10)
    [InlineData("100.127.255.255", false)] // End of CGNAT
    [InlineData("100.128.0.0", true)] // Just after CGNAT
    [InlineData("0.0.0.0", false)] // Unspecified
    [InlineData("255.255.255.255", true)] // Broadcast — not private; outbound is typically safe
    public void IsAllowed_ShouldBeExact_AtRangeBoundaries(string ipAddress, bool expected)
    {
        // Arrange
        DefaultNetworkSecurityPolicy policy = CreatePolicy(
            allowLocal: false,
            environment: "Production"
        );
        IPAddress address = IPAddress.Parse(ipAddress);

        // Act
        bool result = policy.IsAllowed(address);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("fd00::1", false)] // Unique Local Address (ULA)
    [InlineData("fe80::1", false)] // Link-Local
    [InlineData("::ffff:127.0.0.1", false)] // IPv4-mapped loopback
    public void IsAllowed_ShouldHandleIPv6SpecialRanges(string ipAddress, bool expected)
    {
        // Arrange
        DefaultNetworkSecurityPolicy policy = CreatePolicy(
            allowLocal: false,
            environment: "Production"
        );
        IPAddress address = IPAddress.Parse(ipAddress);

        // Act
        bool result = policy.IsAllowed(address);

        // Assert
        Assert.Equal(expected, result);
    }
}
