using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export.Capabilities;

/// <summary>
/// Static capability descriptor for native codebook exports.
/// </summary>
internal static class NativeCodebookExportCapability
{
    internal static readonly ExportCapability _value = new(
        ExportTarget.Codebook,
        ExportDeliveryFormat.Json,
        ExportProfile.Native,
        WireKey: "codebook",
        Label: "Codebook",
        ItemTypeName: typeof(DynamicExportRow).FullName!);
}
