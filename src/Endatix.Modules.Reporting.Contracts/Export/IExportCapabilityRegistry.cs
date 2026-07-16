namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Single source of truth for supported export capabilities.
/// </summary>
public interface IExportCapabilityRegistry
{
    /// <summary>
    /// Gets all export capabilities.
    /// </summary>
    /// <returns>A list of export capabilities.</returns>
    IReadOnlyList<ExportCapability> GetAll();

    /// <summary>
    /// Tries to get an export capability by its wire key.
    /// </summary>
    /// <param name="wireKey">The wire key of the export capability.</param>
    /// <param name="capability">The export capability, or null if not found.</param>
    /// <returns>True if the export capability was found, false if not.</returns>
    bool TryGetByWireKey(string wireKey, out ExportCapability capability);


    /// <summary>
    /// Tries to get an export capability by its target, delivery format, and profile.
    /// </summary>
    /// <param name="target">The target of the export capability.</param>
    /// <param name="deliveryFormat">The delivery format of the export capability.</param>
    /// <param name="profile">The profile of the export capability.</param>
    /// <param name="capability">The export capability, or null if not found.</param>
    /// <returns>True if the export capability was found, false if not.</returns>
    bool TryGet(
        ExportTarget target,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile,
        out ExportCapability capability);

    /// <summary>
    /// Checks if an export capability is valid.
    /// </summary>
    /// <param name="target">The target of the export capability.</param>
    /// <param name="deliveryFormat">The delivery format of the export capability.</param>
    /// <param name="profile">The profile of the export capability.</param>
    /// <returns>True if the export capability is valid, false if not.</returns>
    bool IsValid(ExportTarget target, ExportDeliveryFormat deliveryFormat, ExportProfile profile);

    /// <summary>
    /// Converts an export capability to a wire key.
    /// </summary>
    /// <param name="target">The target of the export capability.</param>
    /// <param name="deliveryFormat">The delivery format of the export capability.</param>
    /// <param name="profile">The profile of the export capability.</param>
    /// <returns>The wire key of the export capability.</returns>
    string ToWireKey(ExportTarget target, ExportDeliveryFormat deliveryFormat, ExportProfile profile);

    /// <summary>
    /// Checks if an export capability matches a wire key and item type.
    /// </summary>
    /// <param name="wireKey">The wire key of the export capability.</param>
    /// <param name="itemType">The item type of the export capability.</param>
    /// <returns>True if the export capability matches the wire key and item type, false if not.</returns>
    bool Matches(string wireKey, Type itemType);
}
