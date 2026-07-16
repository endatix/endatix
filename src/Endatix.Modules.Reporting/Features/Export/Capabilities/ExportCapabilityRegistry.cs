using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

namespace Endatix.Modules.Reporting.Features.Export.Capabilities;

/// <summary>
/// Registry of supported export capabilities.
/// </summary>
internal sealed class ExportCapabilityRegistry : IExportCapabilityRegistry
{
    private static readonly IReadOnlyList<ExportCapability> _capabilities =
    [
        ..SubmissionsExportCapabilities.All,
        NativeCodebookExportCapability._value,
        ShojiCodebookExportCapability._value,
    ];

    private readonly Dictionary<string, ExportCapability> _byWireKey =
        _capabilities.ToDictionary(capability => capability.WireKey, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<(ExportTarget, ExportDeliveryFormat, ExportProfile), ExportCapability> _byTuple =
        _capabilities.ToDictionary(capability => (capability.Target, capability.DeliveryFormat, capability.Profile));

    public IReadOnlyList<ExportCapability> GetAll() => _capabilities;

    public bool TryGetByWireKey(string wireKey, out ExportCapability capability)
    {
        if (string.IsNullOrWhiteSpace(wireKey))
        {
            capability = default!;
            return false;
        }

        return _byWireKey.TryGetValue(wireKey.Trim(), out capability!);
    }

    public bool TryGet(
        ExportTarget target,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile,
        out ExportCapability capability) =>
        _byTuple.TryGetValue((target, deliveryFormat, profile), out capability!);

    public bool IsValid(ExportTarget target, ExportDeliveryFormat deliveryFormat, ExportProfile profile) =>
        _byTuple.ContainsKey((target, deliveryFormat, profile));

    public string ToWireKey(ExportTarget target, ExportDeliveryFormat deliveryFormat, ExportProfile profile)
    {
        if (!TryGet(target, deliveryFormat, profile, out var capability))
        {
            throw new ArgumentException(
                $"Unsupported export capability: target={target}, delivery={deliveryFormat}, profile={profile}.");
        }

        return capability.WireKey;
    }

    public bool Matches(string wireKey, Type itemType)
    {
        if (!TryGetByWireKey(wireKey, out var capability))
        {
            return false;
        }

        return capability.ItemTypeName == itemType.FullName;
    }
}
