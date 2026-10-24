// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Sockets;

namespace EchoPhase.Runners.Blocks.Http
{
    /// <summary>
    /// SSRF guard for program requests: resolves the host itself and connects
    /// only to public addresses, so neither a hostname pointing at 127.0.0.1
    /// nor a redirect to 169.254.169.254 (cloud metadata) gets through. The
    /// check is on the address actually dialed, which defeats DNS rebinding.
    /// </summary>
    public static class PublicNetworkConnector
    {
        public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Create(bool allowPrivate)
            => async (context, ct) =>
            {
                var endpoint = context.DnsEndPoint;
                var addresses = IPAddress.TryParse(endpoint.Host, out var literal)
                    ? [literal]
                    : await Dns.GetHostAddressesAsync(endpoint.Host, ct);

                var allowed = addresses.Where(a => allowPrivate || IsPublic(a)).ToArray();
                if (allowed.Length == 0)
                    throw new HttpRequestException(
                        $"'{endpoint.Host}' is not a public address; requests to private networks are not allowed.");

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(allowed, endpoint.Port, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };

        /// <summary>Not loopback, private, link-local, shared, multicast, documentation or reserved.</summary>
        public static bool IsPublic(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                return !(b[0] is 0 or 10 or 127 or >= 224
                         || (b[0] == 100 && b[1] is >= 64 and <= 127)
                         || (b[0] == 169 && b[1] == 254)
                         || (b[0] == 172 && b[1] is >= 16 and <= 31)
                         || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                         || (b[0] == 192 && b[1] == 168)
                         || (b[0] == 198 && b[1] is 18 or 19)
                         || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                         || (b[0] == 203 && b[1] == 0 && b[2] == 113));
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any)
                    || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                    return false;

                var b = address.GetAddressBytes();
                return !((b[0] & 0xFE) == 0xFC                                  // fc00::/7 unique local
                         || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // 2001:db8::/32 documentation
                         || (b[0] == 0x20 && b[1] == 0x02)                        // 2002::/16 6to4 (wraps IPv4)
                         || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B)); // 64:ff9b::/96 NAT64 (wraps IPv4)
            }

            return false;
        }
    }
}
