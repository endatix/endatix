using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

internal static class TestExportCapabilityRegistry
{
    internal static IExportCapabilityRegistry Instance { get; } = new ExportCapabilityRegistry();
}
