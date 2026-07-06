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
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(formDefinitionRevision);
        Guard.Against.NullOrEmpty(schemaJson);

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
    /// Append-only form schema column specification as JSON.
    /// </summary>
    public string SchemaJson { get; private set; } = "[]";

    public void UpdateSchema(long formDefinitionRevision, string schemaJson)
    {
        Guard.Against.NegativeOrZero(formDefinitionRevision);
        Guard.Against.NullOrEmpty(schemaJson);

        if (formDefinitionRevision < FormDefinitionRevision)
        {
            throw new InvalidOperationException(
                $"Cannot roll back FormDefinitionRevision from {FormDefinitionRevision} to {formDefinitionRevision}.");
        }

        FormDefinitionRevision = formDefinitionRevision;
        SchemaJson = schemaJson;
    }
}
