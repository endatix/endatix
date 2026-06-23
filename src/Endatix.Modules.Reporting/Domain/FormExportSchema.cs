using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Compiled column specification for a form, used by exports and submission flattening.
/// One row per tenant + form (upserted on definition changes).
/// </summary>
public sealed class FormExportSchema : BaseEntity, ITenantOwned, IAggregateRoot
{
    private FormExportSchema() { }

    public FormExportSchema(
        long tenantId,
        long formId,
        long formDefinitionRevision,
        string schemaJson)
    {
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(formDefinitionRevision, nameof(formDefinitionRevision));
        Guard.Against.NullOrEmpty(schemaJson, nameof(schemaJson));

        TenantId = tenantId;
        FormId = formId;
        FormDefinitionRevision = formDefinitionRevision;
        SchemaJson = schemaJson;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>
    /// Revision of the source form definition at compile time. Distinct from
    /// <see cref="IHasRevision"/> aggregate revision on core entities.
    /// </summary>
    public long FormDefinitionRevision { get; private set; }

    /// <summary>
    /// Append-only column specification as JSON (codebook entries).
    /// </summary>
    public string SchemaJson { get; private set; } = "[]";

    public void UpdateSchema(long formDefinitionRevision, string schemaJson)
    {
        Guard.Against.NegativeOrZero(formDefinitionRevision, nameof(formDefinitionRevision));
        Guard.Against.NullOrEmpty(schemaJson, nameof(schemaJson));

        FormDefinitionRevision = formDefinitionRevision;
        SchemaJson = schemaJson;
    }
}
