using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Reporting.Domain;

/// <summary>
/// Compiled column specification for a form, used by exports and submission flattening.
/// One row per tenant + form (saved when the active definition changes).
/// </summary>
public sealed class FormSchema : BaseEntity, ITenantOwned, IAggregateRoot
{
    public const string EmptyFlatteningMapJson = """{"version":1,"columns":[]}""";
    public const string EmptyCodebookJson = """{"version":1,"locales":["default"],"questions":{},"columns":{},"choiceCatalogs":{}}""";
    public const string EmptyLocalesJson = """["default"]""";
    /// <summary>The search keys of a form that has none: an empty JSON array.</summary>
    public const string EmptySearchKeysJson = "[]";

    private FormSchema() { }

    public FormSchema(
        long tenantId,
        long formId,
        long formDefinitionRevision,
        string flatteningMap,
        string codebook,
        string? locales = null)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NegativeOrZero(formDefinitionRevision);
        Guard.Against.NullOrEmpty(flatteningMap);
        Guard.Against.NullOrEmpty(codebook);

        TenantId = tenantId;
        FormId = formId;
        FormDefinitionRevision = formDefinitionRevision;
        FlatteningMap = flatteningMap;
        Codebook = codebook;
        Locales = NormalizeLocalesJson(locales);
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>
    /// Revision of the source form definition at compile time. Distinct from
    /// <see cref="IHasRevision"/> aggregate revision on core entities.
    /// </summary>
    public long FormDefinitionRevision { get; private set; }

    /// <summary>
    /// Append-only flattening map: ordered export columns and extraction rules.
    /// </summary>
    public string FlatteningMap { get; private set; } = EmptyFlatteningMapJson;

    /// <summary>
    /// SurveyJS-aligned metadata for BI export and Shoji projection.
    /// </summary>
    public string Codebook { get; private set; } = EmptyCodebookJson;

    /// <summary>
    /// Discovered SurveyJS locales for this form (JSON string array). Replaced on each compile.
    /// </summary>
    public string Locales { get; private set; } = EmptyLocalesJson;

    /// <summary>
    /// Append-only list of the keys copied into each flattened row's search copy, the only keys answer filters read
    /// (JSON array of <c>{ key, source, filterType, addedAt }</c>).
    /// </summary>
    public string SearchKeys { get; private set; } = EmptySearchKeysJson;

    /// <summary>Replaces the search keys; the caller has applied the append-only and cap rules.</summary>
    public void UpdateSearchKeys(string searchKeys)
    {
        Guard.Against.NullOrWhiteSpace(searchKeys);
        SearchKeys = searchKeys;
    }

    public void UpdateSchema(
        long formDefinitionRevision,
        string flatteningMap,
        string codebook,
        string? locales = null)
    {
        Guard.Against.NegativeOrZero(formDefinitionRevision);
        Guard.Against.NullOrEmpty(flatteningMap);
        Guard.Against.NullOrEmpty(codebook);

        if (formDefinitionRevision < FormDefinitionRevision)
        {
            throw new InvalidOperationException(
                $"Cannot roll back FormDefinitionRevision from {FormDefinitionRevision} to {formDefinitionRevision}.");
        }

        FormDefinitionRevision = formDefinitionRevision;
        FlatteningMap = flatteningMap;
        Codebook = codebook;
        Locales = NormalizeLocalesJson(locales);
    }

    /// <summary>
    /// Moves the revision back to an older definition's, for a schema about to be replaced with one compiled from that
    /// definition alone: the columns of any newer definition are gone, so the schema must no longer claim it.
    /// </summary>
    public void RevertRevisionTo(long formDefinitionRevision)
    {
        Guard.Against.NegativeOrZero(formDefinitionRevision);
        FormDefinitionRevision = Math.Min(FormDefinitionRevision, formDefinitionRevision);
    }

    private static string NormalizeLocalesJson(string? locales) =>
        string.IsNullOrWhiteSpace(locales) ? EmptyLocalesJson : locales;
}
