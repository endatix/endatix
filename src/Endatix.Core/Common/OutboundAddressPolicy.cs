namespace Endatix.Core.Common;

/// <summary>
/// Which addresses an outbound call to a tenant-configured URL may reach. One value, built once from server
/// configuration (never from tenant settings or the API), read by the save-time rule and the connect-time check.
/// </summary>
/// <param name="AllowPrivateNetworkTargets">
/// True to allow private and local network addresses and reserved names (see <see cref="PublicNetworkAddress"/>).
/// </param>
public sealed record OutboundAddressPolicy(bool AllowPrivateNetworkTargets)
{
    /// <summary>The default: public network addresses only.</summary>
    public static OutboundAddressPolicy PublicOnly { get; } = new(false);
}
