namespace Endatix.Infrastructure.Networking;

/// <summary>Per-client guard state, named by the HTTP client name. Unset for clients without the guard.</summary>
internal sealed class PublicAddressGuardOptions
{
    /// <summary>Whether the client connects only through <see cref="PublicAddressConnector"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Resolves a host name; null for DNS. Set only by tests, to send a name to an address of their choice.</summary>
    public Func<string, CancellationToken, Task<System.Net.IPAddress[]>>? Resolve { get; set; }
}
