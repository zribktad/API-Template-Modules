using System.Net;
using System.Net.Sockets;

namespace Webhooks.Security;

/// <summary>
/// Factory for creating a <see cref="SocketsHttpHandler"/> that prevents SSRF TOCTOU attacks by validating
/// resolved IP addresses immediately before establishing a connection and pinning the socket to a validated IP.
/// It also supports connecting to alternate IP addresses if the primary one fails.
/// </summary>
internal static class SsrfProtectedSocketsHttpHandlerFactory
{
    public static SocketsHttpHandler Create(INetworkSecurityPolicy securityPolicy)
    {
        return new SocketsHttpHandler
        {
            // Bound connection pool age so DNS changes are re-resolved for long-lived HttpClient instances.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, ct) =>
            {
                // 1. Resolve DNS authoritative results
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(
                    context.DnsEndPoint.Host,
                    ct
                );

                // 2. Filter addresses through the security policy
                List<IPAddress> allowedAddresses = addresses
                    .Where(securityPolicy.IsAllowed)
                    .ToList();

                if (allowedAddresses.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Connection to '{context.DnsEndPoint.Host}' is prohibited. "
                            + $"All resolved addresses ({string.Join(", ", (IEnumerable<IPAddress>)addresses)}) are restricted by the network security policy."
                    );
                }

                // 3. Attempt to connect iteratively to allowed addresses (supports Dual-Stack/Fallback)
                Exception? lastException = null;
                foreach (IPAddress address in allowedAddresses)
                {
                    Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(
                            new IPEndPoint(address, context.DnsEndPoint.Port),
                            ct
                        );
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        socket.Dispose();
                        lastException = ex;
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }

                throw new InvalidOperationException(
                    $"Failed to connect to any of the allowed IP addresses for '{context.DnsEndPoint.Host}'.",
                    lastException
                );
            },
        };
    }
}
