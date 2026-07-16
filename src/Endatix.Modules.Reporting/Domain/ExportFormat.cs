using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Tenant-scoped export format definition. Row data is always sourced from the reporting read model.
/// </summary>
public sealed class ExportFormat : BaseEntity, ITenantOwned, IAggregateRoot
{
    public const int NAME_MAX_LENGTH = 200;
    public const int DESCRIPTION_MAX_LENGTH = 500;
    public const int EXPORT_TARGET_MAX_LENGTH = 32;
    public const int DELIVERY_FORMAT_MAX_LENGTH = 16;
    public const int PROFILE_MAX_LENGTH = 16;

    private ExportFormat() { }

    public ExportFormat(
        long tenantId,
        string name,
        ExportTarget exportTarget,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile = ExportProfile.Native,
        string? description = null)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NullOrEmpty(name);
        Guard.Against.InvalidInput(name, nameof(name), n => n.Length <= NAME_MAX_LENGTH);
        Guard.Against.InvalidInput(exportTarget, nameof(exportTarget), Enum.IsDefined);
        Guard.Against.InvalidInput(deliveryFormat, nameof(deliveryFormat), Enum.IsDefined);
        Guard.Against.InvalidInput(profile, nameof(profile), Enum.IsDefined);
        Guard.Against.InvalidInput(description, nameof(description), d => d is null || d.Length <= DESCRIPTION_MAX_LENGTH);

        TenantId = tenantId;
        Name = name;
        ExportTarget = exportTarget;
        DeliveryFormat = deliveryFormat;
        Profile = profile;
        Description = description;
    }

    public long TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ExportTarget ExportTarget { get; private set; }

    public ExportDeliveryFormat DeliveryFormat { get; private set; }

    public ExportProfile Profile { get; private set; }

    public string? Description { get; private set; }

    public string? SettingsJson { get; private set; }

    public void UpdateName(string name)
    {
        Guard.Against.NullOrEmpty(name);
        Guard.Against.InvalidInput(name, nameof(name), n => n.Length <= NAME_MAX_LENGTH);

        Name = name;
    }

    public void UpdateDescription(string? description)
    {
        Guard.Against.InvalidInput(description, nameof(description), d => d is null || d.Length <= DESCRIPTION_MAX_LENGTH);

        Description = description;
    }

    public void UpdateSettingsJson(string? settingsJson) => SettingsJson = settingsJson;
}
