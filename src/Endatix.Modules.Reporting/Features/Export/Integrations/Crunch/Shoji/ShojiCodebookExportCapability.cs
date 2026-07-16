using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Static capability descriptor for Crunch Shoji codebook exports.
/// </summary>
internal static class ShojiCodebookExportCapability
{
    internal static readonly ExportCapability _value = new(
        ExportTarget.Codebook,
        ExportDeliveryFormat.Json,
        ExportProfile.Shoji,
        WireKey: "codebook-shoji",
        Label: "Codebook (Shoji)",
        ItemTypeName: typeof(DynamicExportRow).FullName!);
}
