using System.Net;

namespace APITemplate.Tests.Unit.TestData;

/// <summary>Private / link-local IPv4 strings for SSRF guard tests.</summary>
public static class SsrfTheoryData
{
    public static readonly string[] ProhibitedIpv4List =
    [
        "127.0.0.1",
        "10.0.0.1",
        "172.16.0.1",
        "172.31.255.255",
        "192.168.1.1",
        "169.254.1.1",
    ];

    public static IEnumerable<object[]> PrivateIpv4Cases() => MapProhibitedIpv4(ip => ip);

    public static IEnumerable<object[]> ProhibitedPrivateIpv4Addresses() =>
        MapProhibitedIpv4(IPAddress.Parse);

    private static IEnumerable<object[]> MapProhibitedIpv4<T>(Func<string, T> map) =>
        ProhibitedIpv4List.Select(ip => new object[] { map(ip) });
}
