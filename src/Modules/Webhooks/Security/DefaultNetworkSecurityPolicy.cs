using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Webhooks.Contracts;

namespace Webhooks.Security;

/// <summary>
/// Default implementation of <see cref="INetworkSecurityPolicy"/> that prohibits requests to
/// loopback, private, link-local, and unspecified addresses (e.g., 0.0.0.0, ::) unless configured otherwise.
/// </summary>
internal sealed class DefaultNetworkSecurityPolicy(
    IHostEnvironment env,
    IOptions<WebhookOptions> options
) : INetworkSecurityPolicy
{
    public bool IsAllowed(IPAddress address)
    {
        // Defense in depth: Allow local requests ONLY in Development environment AND if explicitly enabled in config.
        if (env.IsDevelopment() && options.Value.AllowLocalRequests)
            return true;

        // 1. Block loopback (127.0.0.1, ::1)
        if (IPAddress.IsLoopback(address))
            return false;

        // 2. Block unspecified/any addresses (0.0.0.0, ::) which can resolve to localhost in some environments
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        // 3. Handle IPv6 specifics
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || address.IsIPv6SiteLocal)
                return false;
        }

        // 4. Normalize IPv4-mapped IPv6 to prevent bypasses like ::ffff:10.0.0.1
        IPAddress ipv4 = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (ipv4.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> bytes = stackalloc byte[4];
            ipv4.TryWriteBytes(bytes, out _);

            // Block private ranges and IANA special-purpose networks.
            return bytes[0] switch
            {
                0 => false, // 0.0.0.0/8 ("this network" — whole block, not just 0.0.0.0)
                10 => false, // 10.0.0.0/8 (Private)
                100 => bytes[1] < 64 || bytes[1] > 127, // 100.64.0.0/10 (Carrier-grade NAT)
                169 => bytes[1] != 254, // 169.254.0.0/16 (Link-local) - block if 254
                172 => bytes[1] < 16 || bytes[1] > 31, // 172.16.0.0/12 (Private)
                // 192.168.0.0/16 (Private) and 192.0.0.0/24 (IETF Protocol Assignments)
                192 => bytes[1] != 168 && !(bytes[1] == 0 && bytes[2] == 0),
                198 => bytes[1] != 18 && bytes[1] != 19, // 198.18.0.0/15 (benchmarking)
                // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved (includes 255.255.255.255 broadcast)
                >= 224 => false,
                _ => true,
            };
        }

        return true;
    }
}
