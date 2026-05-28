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
                    ForwardedHeaders.XForwardedProto;

                // Trust-all is intentionally limited to Development. Production keeps ASP.NET Core's
                // known proxy/network restrictions unless the host configures them explicitly.
                if (reverseProxy.TrustAllProxiesInDevelopment && environment.IsDevelopment())
                {
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                }
            });

        return services;
    }
}
