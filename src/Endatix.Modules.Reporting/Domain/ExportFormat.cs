using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Tenant-scoped export format definition. Row data is always sourced from the reporting read model.
/// </summary>
public sealed class ExportFormat : BaseEntity, ITenantOwned, IAggregateRoot
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 500;
    public const int SerializationTypeMaxLength = 32;

    private ExportFormat() { }

    public ExportFormat(
        long tenantId,
        string name,
        ExportSerializationType serializationType,
        string? description = null)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NullOrEmpty(name);
        Guard.Against.InvalidInput(name, nameof(name), n => n.Length <= NameMaxLength);
        Guard.Against.InvalidInput(serializationType, nameof(serializationType), Enum.IsDefined);
        Guard.Against.InvalidInput(description, nameof(description), d => d is null || d.Length <= DescriptionMaxLength);

        TenantId = tenantId;
        Name = name;
        SerializationType = serializationType;
        Description = description;
    }

    public long TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ExportSerializationType SerializationType { get; private set; }

    public string? Description { get; private set; }
}
