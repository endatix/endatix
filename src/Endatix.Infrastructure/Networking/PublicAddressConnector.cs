using System.Net;
using System.Net.Sockets;
using Endatix.Core.Common;

namespace Endatix.Infrastructure.Networking;

/// <summary>
/// The connect callback of a guarded client: judges the host, resolves it once, checks every address and connects
/// to a checked one, so a name that answers differently later gets nothing.
/// </summary>
/// <param name="policy">Whether private network targets are allowed.</param>
/// <param name="resolve">Resolves a host name; <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)"/> by default.</param>
internal sealed class PublicAddressConnector(
    OutboundAddressPolicy policy,
    Func<string, CancellationToken, Task<IPAddress[]>>? resolve = null)
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve = resolve ?? Dns.GetHostAddressesAsync;

    /// <summary>The <see cref="SocketsHttpHandler.ConnectCallback"/>.</summary>
    public ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken) =>
        ConnectAsync(context.DnsEndPoint, cancellationToken);

    /// <summary>
    /// Connects to <paramref name="endPoint"/>. Throws <see cref="OutboundAddressRefusedException"/> for a host that is
    /// not public while private targets are not allowed; a resolver failure propagates as it is.
    /// </summary>
    public async ValueTask<Stream> ConnectAsync(DnsEndPoint endPoint, CancellationToken cancellationToken)
    {
        var addresses = await AddressesOfAsync(endPoint.Host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        RefuseUnlessAllPublic(addresses);
        return await ConnectToFirstAsync(addresses, endPoint.Port, cancellationToken);
    }

    private async Task<IPAddress[]> AddressesOfAsync(string host, CancellationToken cancellationToken)
    {
        if (PublicNetworkAddress.TryParseLiteral(host, out var literal))
        {
            return [literal];
        }

        if (!policy.AllowPrivateNetworkTargets && PublicNetworkAddress.IsKnownPrivateHost(host))
        {
            throw new OutboundAddressRefusedException(OutboundAddressRefusalReason.ReservedName);
        }

        return await _resolve(host, cancellationToken);
    }

    private void RefuseUnlessAllPublic(IPAddress[] addresses)
    {
        if (!policy.AllowPrivateNetworkTargets && !addresses.All(PublicNetworkAddress.IsPublic))
        {
            throw new OutboundAddressRefusedException(OutboundAddressRefusalReason.NonPublicAddress);
        }
    }

    // One socket per address, in the order the resolver gave them; the last failure is the one reported.
    private static async Task<Stream> ConnectToFirstAsync(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        SocketException? lastFailure = null;
        foreach (var address in addresses)
        {
            try
            {
                // An IPv4-mapped address was judged as its IPv4 address, so that is what is connected to.
                var target = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
                return await ConnectAsync(new IPEndPoint(target, port), cancellationToken);
            }
            catch (SocketException failure)
            {
                lastFailure = failure;
            }
        }

        throw lastFailure!;
    }

    private static async Task<Stream> ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endPoint, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
