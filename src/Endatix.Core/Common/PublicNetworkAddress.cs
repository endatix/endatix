using System.Net;
using System.Net.Sockets;

namespace Endatix.Core.Common;

/// <summary>
/// Decides whether an address or a host name belongs to the public internet. It holds the one list of non-public
/// ranges and reserved names: the save-time rule and the connect-time check both read it.
/// </summary>
/// <remarks>
/// <para>
/// IPv4: 0.0.0.0/8, 10/8, 100.64/10, 127/8, 169.254/16, 172.16/12, 192.0.0/24, 192.0.2/24, 192.168/16, 198.18/15,
/// 198.51.100/24, 203.0.113/24, 224/4 and 240/4 (which holds 255.255.255.255).
/// </para>
/// <para>
/// IPv6: ::/128, ::1/128, fc00::/7, fe80::/10, fec0::/10, ff00::/8, 2001:db8::/32 and 100::/64. These prefixes carry an
/// IPv4 address and are not public whatever they carry: IPv4-compatible ::/96, IPv4-translated ::ffff:0:0:0/96, NAT64
/// 64:ff9b::/96 and 64:ff9b:1::/48, 6to4 2002::/16 and Teredo 2001::/32. An IPv4-mapped address (::ffff:0:0/96) is judged
/// as the IPv4 address it carries. An address with a scope is not public.
/// </para>
/// </remarks>
public static class PublicNetworkAddress
{
    private static readonly AddressRange[] IPv4Ranges =
    [
        new([0, 0, 0, 0], 8),
        new([10, 0, 0, 0], 8),
        new([100, 64, 0, 0], 10),
        new([127, 0, 0, 0], 8),
        new([169, 254, 0, 0], 16),
        new([172, 16, 0, 0], 12),
        new([192, 0, 0, 0], 24),
        new([192, 0, 2, 0], 24),
        new([192, 168, 0, 0], 16),
        new([198, 18, 0, 0], 15),
        new([198, 51, 100, 0], 24),
        new([203, 0, 113, 0], 24),
        new([224, 0, 0, 0], 4),
        new([240, 0, 0, 0], 4),
    ];

    // Leading 16-bit groups of each network; the rest is zero.
    private static readonly AddressRange[] IPv6Ranges =
    [
        V6(96),                  // unspecified, loopback and IPv4-compatible
        V6(7, 0xfc00),
        V6(10, 0xfe80),
        V6(10, 0xfec0),
        V6(8, 0xff00),
        V6(32, 0x2001, 0x0db8),
        V6(64, 0x0100),
        V6(96, 0x0064, 0xff9b),
        V6(48, 0x0064, 0xff9b, 0x0001),
        V6(16, 0x2002),
        V6(32, 0x2001, 0x0000),
        V6(96, 0, 0, 0, 0, 0xffff, 0),   // IPv4-translated
    ];

    // "metadata.google.internal" is covered by ".internal" and "*.svc.cluster.local" by ".local"; both are kept
    // so the list reads as the decision.
    private static readonly string[] ReservedNames = ["localhost", "metadata.google.internal"];
    private static readonly string[] ReservedSuffixes = [".localhost", ".internal", ".local", ".svc.cluster.local"];

    /// <summary>True when <paramref name="address"/> is a public unicast address.</summary>
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublic(address.MapToIPv4());
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !InAny(address, IPv4Ranges),
            AddressFamily.InterNetworkV6 => address.ScopeId == 0 && !InAny(address, IPv6Ranges),
            _ => false,
        };
    }

    /// <summary>
    /// True when <paramref name="host"/> is known to be non-public without asking DNS: an IP literal that is not
    /// public, or a reserved name such as <c>localhost</c> or <c>*.internal</c>. False for every other name, which
    /// only the connect-time check can judge.
    /// </summary>
    /// <param name="host">A URI host. Brackets, an IPv6 zone and one trailing dot are accepted; case is ignored.</param>
    public static bool IsKnownPrivateHost(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var normalized = Normalize(host);
        return TryParseAddress(normalized, out var address)
            ? !IsPublic(address)
            : IsReservedName(normalized);
    }

    /// <summary>
    /// Reads <paramref name="host"/> as an IP literal the way <see cref="IsKnownPrivateHost"/> does: brackets, an IPv6
    /// zone and one trailing dot are accepted.
    /// </summary>
    public static bool TryParseLiteral(string host, out IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(host);
        return TryParseAddress(Normalize(host), out address);
    }

    private static bool TryParseAddress(string normalized, out IPAddress address)
    {
        if (IPAddress.TryParse(normalized, out var parsed))
        {
            address = parsed;
            return true;
        }

        address = IPAddress.None;
        return false;
    }

    // Brackets and one trailing dot go; the zone names a local interface, so only the address is judged.
    private static string Normalize(string host)
    {
        var normalized = host.Trim();
        normalized = normalized.StartsWith('[') && normalized.EndsWith(']') ? normalized[1..^1] : normalized;
        var zone = normalized.IndexOf('%');
        normalized = zone >= 0 ? normalized[..zone] : normalized;
        normalized = normalized.EndsWith('.') ? normalized[..^1] : normalized;
        return normalized.ToLowerInvariant();
    }

    private static bool IsReservedName(string normalizedHost) =>
        ReservedNames.Contains(normalizedHost, StringComparer.Ordinal)
        || ReservedSuffixes.Any(suffix => normalizedHost.EndsWith(suffix, StringComparison.Ordinal));

    private static bool InAny(IPAddress address, AddressRange[] ranges)
    {
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out var written);
        var value = bytes[..written];
        foreach (var range in ranges)
        {
            if (range.Contains(value))
            {
                return true;
            }
        }

        return false;
    }

    private static AddressRange V6(int prefixLength, params ushort[] leadingGroups)
    {
        var network = new byte[16];
        for (var group = 0; group < leadingGroups.Length; group++)
        {
            network[2 * group] = (byte)(leadingGroups[group] >> 8);
            network[(2 * group) + 1] = (byte)leadingGroups[group];
        }

        return new AddressRange(network, prefixLength);
    }

    private readonly record struct AddressRange(byte[] Network, int PrefixLength)
    {
        public bool Contains(ReadOnlySpan<byte> address)
        {
            var fullBytes = PrefixLength / 8;
            var remainingBits = PrefixLength % 8;
            return address.Length == Network.Length
                && address[..fullBytes].SequenceEqual(Network.AsSpan(0, fullBytes))
                && (remainingBits == 0 || SameHighBits(address[fullBytes], Network[fullBytes], remainingBits));
        }

        private static bool SameHighBits(byte left, byte right, int bits)
        {
            var mask = (byte)(0xFF << (8 - bits));
            return (left & mask) == (right & mask);
        }
    }
}
