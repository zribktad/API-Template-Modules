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
        // Double-lock: Allow local requests ONLY in Development environment AND if explicitly enabled in config.
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
            byte[] bytes = ipv4.GetAddressBytes();

            // Block private ranges and restricted networks
            return bytes[0] switch
            {
                10 => false, // 10.0.0.0/8 (Private)
                100 => (bytes[1] & 0xC0) != 64, // 100.64.0.0/10 (Carrier-grade NAT) - block if it IS 64
                172 => bytes[1] < 16 || bytes[1] > 31, // 172.16.0.0/12 (Private) - block if in range
                192 => bytes[1] != 168, // 192.168.0.0/16 (Private) - block if 168
                169 => bytes[1] != 254, // 169.254.0.0/16 (Link-local) - block if 254
                _ => true,
            };
        }

        return true;
    }
}
