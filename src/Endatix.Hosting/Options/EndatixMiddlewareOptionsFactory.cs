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
            // Bound, not defaulted: UseDefaults() routes through this factory, so a path left out
            // here can never be changed from configuration no matter what the options object says.
            HealthCheckPath = options.HealthCheckPath,
            LivenessPath = options.LivenessPath,
            ReadinessPath = options.ReadinessPath,
            ApiOptions = apiOptions ?? new ApiOptions()
        };
    }
}
