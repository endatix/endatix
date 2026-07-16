using Endatix.Modules.Reporting.Contracts.Export;
using FastEndpoints;

namespace Endatix.Modules.Reporting.Infrastructure.Serialization;
/// <summary>
/// Registers Reporting-specific JSON converters for FastEndpoints.
/// </summary>
public static class ReportingJsonSerializerConfiguration
{
    public static void Configure(Config config)
    {
        config.Serializer.Options.Converters.Add(new ColumnAliasProfileJsonConverter());
    }
}
