using System.Reflection;
using Endatix.Modules.Reporting.Infrastructure.Serialization;
using FastEndpoints;

namespace Endatix.Modules.Reporting.Infrastructure;

/// <summary>
/// FastEndpoints registration hooks for the reporting module.
/// </summary>
internal static class ReportingModuleEndpointConfiguration
{
    internal const string OpenApiTag = "reporting";

    private static readonly Assembly _reportingAssembly = typeof(ReportingModule).Assembly;

    internal static void Configure(Config config)
    {
        ReportingJsonSerializerConfiguration.Configure(config);

        config.Endpoints.Configurator = endpoint =>
        {
            if (endpoint.EndpointType?.Assembly != _reportingAssembly)
            {
                return;
            }

            endpoint.Tags(OpenApiTag);
        };
    }
}
