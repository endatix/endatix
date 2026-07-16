using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export.Capabilities;

/// <summary>
/// Static capability descriptors for submission tabular exports.
/// </summary>
internal static class SubmissionsExportCapabilities
{
    internal static readonly ExportCapability _csv = new(
        ExportTarget.Submissions,
        ExportDeliveryFormat.Csv,
        ExportProfile.Native,
        WireKey: "csv",
        Label: "CSV",
        ItemTypeName: typeof(SubmissionExportRow).FullName!);

    internal static readonly ExportCapability _json = new(
        ExportTarget.Submissions,
        ExportDeliveryFormat.Json,
        ExportProfile.Native,
        WireKey: "json",
        Label: "JSON",
        ItemTypeName: typeof(SubmissionExportRow).FullName!);

    internal static IEnumerable<ExportCapability> All => [_csv, _json];
}
