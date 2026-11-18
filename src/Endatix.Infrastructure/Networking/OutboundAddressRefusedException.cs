namespace Endatix.Infrastructure.Networking;

/// <summary>Why the connect-time check refused an endpoint's host.</summary>
public enum OutboundAddressRefusalReason
{
    /// <summary>The host is a reserved name, such as <c>localhost</c> or <c>*.internal</c>; DNS was not asked.</summary>
    ReservedName,

    /// <summary>The host is, or resolved to, an address that is not public.</summary>
    NonPublicAddress,
}

/// <summary>
/// The connect-time check refused to connect because the endpoint's host is not public. Final: retrying cannot change
/// the answer while the configuration stays the same. The message names no host or URL.
/// </summary>
public sealed class OutboundAddressRefusedException : Exception
{
    internal OutboundAddressRefusedException(OutboundAddressRefusalReason reason)
        : base("The endpoint address is not a public network address.")
    {
        Reason = reason;
    }

    /// <summary>Why the host was refused.</summary>
    public OutboundAddressRefusalReason Reason { get; }

    /// <summary>The refusal behind <paramref name="exception"/>, or null when it is not a refusal.</summary>
    /// <remarks>
    /// The handler wraps the refusal in an <see cref="HttpRequestException"/> whose message adds the host and port, so
    /// a caller on this path logs the reason and the endpoint key, never that wrapper.
    /// </remarks>
    public static OutboundAddressRefusedException? Of(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OutboundAddressRefusedException refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="exception"/> or one of its inner exceptions is a refusal.</summary>
    public static bool IsCauseOf(Exception? exception) => Of(exception) is not null;
}
