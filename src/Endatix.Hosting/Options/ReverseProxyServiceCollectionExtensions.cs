using System.Net;
using Endatix.Framework.Configuration;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Hosting.Options;

internal static class ReverseProxyServiceCollectionExtensions
{
    internal static IServiceCollection AddEndatixReverseProxy(this IServiceCollection services)
    {
        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<HostingOptions>, IAppEnvironment>((options, hostingOptions, environment) =>
            {
                var reverseProxy = hostingOptions.Value.ReverseProxy;

                if (!reverseProxy.Enabled)
                {
                    return;
                }

                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedHost |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedPrefix;

                // Trust-all is intentionally limited to Development. Production keeps ASP.NET Core's
                // known proxy/network restrictions unless the host configures them explicitly.
                if (reverseProxy.TrustAllProxiesInDevelopment && environment.IsDevelopment())
                {
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                    return;
                }

                ApplyKnownProxies(options, reverseProxy);
            });

        return services;
    }

    internal const int ForwardLimit = 5;

    private static void ApplyKnownProxies(ForwardedHeadersOptions options, ReverseProxyOptions reverseProxy)
    {
        foreach (var entry in Entries(reverseProxy.KnownProxies))
        {
            if (!IPAddress.TryParse(entry, out var address))
            {
                throw new InvalidOperationException($"KnownProxies value '{entry}' is not an IP address.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var entry in Entries(reverseProxy.KnownNetworks))
        {
            if (!System.Net.IPNetwork.TryParse(entry, out var network))
            {
                throw new InvalidOperationException($"KnownNetworks value '{entry}' is not a CIDR.");
            }

            // Checked on the parsed prefix: "::0/0", "0.0.0.0/00" and "1.2.3.4/0" all parse to a /0.
            if (network.PrefixLength == 0)
            {
                throw new InvalidOperationException($"KnownNetworks value '{entry}' trusts every address (/0).");
            }

            options.KnownIPNetworks.Add(network);
        }

        options.ForwardLimit = ForwardLimit;
    }

    private static IEnumerable<string> Entries(IEnumerable<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim());
}
