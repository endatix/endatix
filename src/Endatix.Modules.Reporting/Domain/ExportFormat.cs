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
    private ExportFormat() { }

    public ExportFormat(
        long tenantId,
        string name,
        ExportSerializationType serializationType,
        string? description = null)
    {
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        Guard.Against.NullOrEmpty(name, nameof(name));

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
