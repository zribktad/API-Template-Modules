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
                // 1. Resolve addresses (handles both DNS names and IP strings)
                // We prefer RequestUri but fall back to DnsEndPoint.
                // context.DnsEndPoint can throw InvalidCastException if the internal endpoint
                // is already an IPEndPoint, so we try to use RequestUri first.
                Uri? uri = context.InitialRequestMessage?.RequestUri;
                string host = uri?.Host ?? context.DnsEndPoint.Host;
                int port = uri?.Port ?? context.DnsEndPoint.Port;

                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, ct);

                // 2. Filter addresses through the security policy
                List<IPAddress> allowedAddresses = addresses
                    .Where(securityPolicy.IsAllowed)
                    .ToList();

                if (allowedAddresses.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Connection to '{host}' is prohibited. "
                            + $"All resolved addresses ({string.Join(", ", addresses)}) are restricted by the network security policy."
                    );
                }

                // 3. Attempt to connect iteratively to allowed addresses (supports Dual-Stack/Fallback)
                Exception? lastException = null;
                foreach (IPAddress address in allowedAddresses)
                {
                    Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, port), ct);
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
                    $"Failed to connect to any of the allowed IP addresses for '{host}'.",
                    lastException
                );
            },
        };
    }
}
