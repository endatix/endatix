using Endatix.Core.Abstractions.Exporting;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Optional export services. Registered so the endpoint constructor stays within the parameter limit
/// while still receiving each service when the reporting module has registered it.
/// </summary>
public sealed class ExportAddons(
    IExportFormatRepository? exportFormatRepository = null,
    IExportCapabilityRegistry? exportCapabilityRegistry = null)
{
    /// <summary>Saved export formats, when the reporting module registered them.</summary>
    public IExportFormatRepository? ExportFormatRepository { get; } = exportFormatRepository;

    /// <summary>Export capabilities, when the reporting module registered them.</summary>
    public IExportCapabilityRegistry? ExportCapabilityRegistry { get; } = exportCapabilityRegistry;
}
