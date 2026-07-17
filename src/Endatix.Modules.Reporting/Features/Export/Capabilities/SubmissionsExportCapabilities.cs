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
        ItemTypeName: typeof(SubmissionExportRow).FullName!,
        Description: "Tabular CSV export with one row per submission.");

    internal static readonly ExportCapability _csvShoji = new(
        ExportTarget.Submissions,
        ExportDeliveryFormat.Csv,
        ExportProfile.Shoji,
        WireKey: "csv-shoji",
        Label: "CSV (Shoji / Crunch)",
        ItemTypeName: typeof(SubmissionExportRow).FullName!,
        Description: "Crunch-compatible CSV: -- key separators and boolean category ids 0/1.");

    internal static readonly ExportCapability _json = new(
        ExportTarget.Submissions,
        ExportDeliveryFormat.Json,
        ExportProfile.Native,
        WireKey: "json",
        Label: "JSON",
        ItemTypeName: typeof(SubmissionExportRow).FullName!,
        Description: "Tabular JSON export with one object per submission.");

    internal static IEnumerable<ExportCapability> All => [_csv, _csvShoji, _json];
}
