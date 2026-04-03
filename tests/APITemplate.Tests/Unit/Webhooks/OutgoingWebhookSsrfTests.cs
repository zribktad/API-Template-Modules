using System.Net;
using System.Reflection;
using Shouldly;
using Webhooks.Shared;
using Xunit;

namespace APITemplate.Tests.Unit.Webhooks;

public sealed class OutgoingWebhookSsrfTests
{
    private static readonly MethodInfo IsProhibitedAddressMethod =
        typeof(OutgoingWebhookBackgroundService).GetMethod(
            "IsProhibitedAddress",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static bool IsProhibited(IPAddress address) =>
        (bool)IsProhibitedAddressMethod.Invoke(null, [address])!;

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    public void IsProhibitedAddress_BlocksPrivateIPv4(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeTrue($"{ip} should be prohibited");
    }

    [Theory]
    [InlineData("::1")]
    public void IsProhibitedAddress_BlocksLoopbackIPv6(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeTrue($"{ip} should be prohibited");
    }

    [Theory]
    [InlineData("fe80::1")]
    public void IsProhibitedAddress_BlocksLinkLocalIPv6(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeTrue($"{ip} should be prohibited");
    }

    [Theory]
    [InlineData("fd00::1")]
    [InlineData("fc00::1")]
    [InlineData("fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void IsProhibitedAddress_BlocksUniqueLocalIPv6(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeTrue($"{ip} should be prohibited (ULA fc00::/7)");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    public void IsProhibitedAddress_AllowsPublicIPv4(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeFalse($"{ip} should be allowed");
    }

    [Theory]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2606:4700:4700::1111")]
    public void IsProhibitedAddress_AllowsPublicIPv6(string ip)
    {
        IsProhibited(IPAddress.Parse(ip)).ShouldBeFalse($"{ip} should be allowed");
    }
}
