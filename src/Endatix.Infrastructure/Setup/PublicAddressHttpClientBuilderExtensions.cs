using Endatix.Core.Common;
using Endatix.Infrastructure.Networking;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the connect-time check on an HTTP client.</summary>
public static class PublicAddressHttpClientBuilderExtensions
{
    /// <summary>
    /// Makes this client connect only to public network addresses: the host is checked before DNS, every resolved
    /// address must be public, and the connection goes to a checked address. Redirects are not followed (a 3xx comes
    /// back as it is) and no proxy is used. A refused connection fails the request with an
    /// <see cref="HttpRequestException"/> caused by <see cref="OutboundAddressRefusedException"/>; a DNS failure stays an
    /// ordinary failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registered <see cref="OutboundAddressPolicy"/> decides whether private network targets are allowed; with
    /// none registered, they are not. The check is applied after the client's own configuration, so a later
    /// <c>ConfigurePrimaryHttpMessageHandler</c> cannot drop it, and a primary handler that is not a
    /// <see cref="SocketsHttpHandler"/> fails the client when it is created.
    /// </para>
    /// <para>
    /// The connect callback covers HTTP/1.1 and HTTP/2. Send requests at those versions (the default), not HTTP/3.
    /// A refusal is final only while no outer retry handler, such as a client default, wraps this client.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder ConnectToPublicAddressesOnly(this IHttpClientBuilder builder)
    {
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());
        builder.Services.TryAddSingleton(OutboundAddressPolicy.PublicOnly);
        builder.Services.AddOptions<PublicAddressGuardOptions>(builder.Name).Configure(guard => guard.Enabled = true);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, PublicAddressGuardFilter>());
        return builder;
    }
}
