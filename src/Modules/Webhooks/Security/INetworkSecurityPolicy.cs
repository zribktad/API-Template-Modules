using System.Net;

namespace Webhooks.Security;

/// <summary>
/// Defines a policy for determining if a destination IP address is allowed for outbound webhook requests.
/// Used to prevent SSRF (Server-Side Request Forgery) by blocking internal and restricted networks.
/// </summary>
public interface INetworkSecurityPolicy
{
    /// <summary>
    /// Returns true if the specified IP address is allowed by the policy; otherwise, false.
    /// </summary>
    bool IsAllowed(IPAddress address);
}
