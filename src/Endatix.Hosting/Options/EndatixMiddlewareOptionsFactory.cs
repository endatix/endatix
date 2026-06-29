using Endatix.Api.Builders;
using Endatix.Framework.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Hosting.Options;

internal static class EndatixMiddlewareOptionsFactory
{
    internal static EndatixMiddlewareOptions Create(IServiceProvider services)
    {
        var hostingOptions = services.GetService<IOptions<HostingOptions>>();
        var apiOptions = services.GetService<IOptions<ApiOptions>>();

        return Create(hostingOptions?.Value, apiOptions?.Value);
    }

    internal static EndatixMiddlewareOptions Create(HostingOptions? hostingOptions, ApiOptions? apiOptions = null)
    {
        var options = hostingOptions ?? new HostingOptions();
        var useForwardedHeaders = options.ReverseProxy.Enabled;

        return new EndatixMiddlewareOptions
        {
            UseForwardedHeaders = useForwardedHeaders,
            UseHsts = options.UseHsts ?? !useForwardedHeaders,
            UseHttpsRedirection = options.UseHttpsRedirection ?? !useForwardedHeaders,
            ApiOptions = apiOptions ?? new ApiOptions()
        };
    }
}
