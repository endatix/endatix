using Endatix.Core.Common;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Networking;

/// <summary>
/// Runs around each client's own handler configuration and, for a guarded client, sets the primary handler's connect
/// callback, redirects and proxy after it, so the client's own configuration cannot undo them.
/// </summary>
internal sealed class PublicAddressGuardFilter(
    IOptionsMonitor<PublicAddressGuardOptions> guards,
    OutboundAddressPolicy policy) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        var guard = guards.Get(builder.Name);
        if (!guard.Enabled)
        {
            return;
        }

        if (builder.PrimaryHandler is not SocketsHttpHandler sockets)
        {
            throw new InvalidOperationException(
                $"HTTP client '{builder.Name}' must use a {nameof(SocketsHttpHandler)} to connect only to public addresses.");
        }

        sockets.AllowAutoRedirect = false;
        sockets.UseProxy = false;
        sockets.Proxy = null;
        sockets.ConnectCallback = new PublicAddressConnector(policy, guard.Resolve).ConnectAsync;
    };
}
