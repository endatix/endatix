using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// The form schema an export runs with: as stored, except that its codebook names and orders the audience block as
/// the Audience tab is now. A rename or reorder never touches the stored schema.
/// </summary>
internal sealed class ExportSchemas(IFormSchemaRepository schemas, IPersonalizationCatalog catalog)
{
    private const string CodebookMetadataKey = "ReportingExportCodebook";

    public Task<FormSchemaEntity?> GetAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        schemas.GetByFormIdAsync(tenantId, formId, cancellationToken);

    /// <summary>One catalog read per call, and none for a form without audience columns.</summary>
    public async Task<string> CodebookAsync(FormSchemaEntity schema, CancellationToken cancellationToken)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        if (!flatteningMap.Columns.Any(column => column.Kind is FormSchemaColumnKind.Personalization))
        {
            return schema.Codebook;
        }

        var variables = await catalog.ListForFormAsync(schema.TenantId, schema.FormId, cancellationToken);
        return PersonalizationCodebook.Overlay(schema.Codebook, flatteningMap, variables);
    }

    /// <summary>
    /// Reads the export codebook while the export is prepared and keeps it in <paramref name="options"/>, so a failed
    /// read fails the request before the download starts.
    /// </summary>
    public async Task PrepareCodebookAsync(FormSchemaEntity schema, ExportOptions options, CancellationToken cancellationToken)
    {
        var codebook = await CodebookAsync(schema, cancellationToken);
        options.Metadata ??= new Dictionary<string, object>();
        options.Metadata[CodebookMetadataKey] = codebook;
    }

    /// <summary>The codebook <see cref="PrepareCodebookAsync"/> kept; read again when the export was not prepared.</summary>
    public async Task<string> PreparedCodebookAsync(
        FormSchemaEntity schema,
        ExportOptions options,
        CancellationToken cancellationToken) =>
        options.Metadata?.TryGetValue(CodebookMetadataKey, out var prepared) == true && prepared is string codebook
            ? codebook
            : await CodebookAsync(schema, cancellationToken);

    /// <summary>A detached copy of <paramref name="schema"/> with <paramref name="codebook"/>; never saved.</summary>
    public static FormSchemaEntity WithCodebook(FormSchemaEntity schema, string codebook) =>
        new(schema.TenantId, schema.FormId, schema.FormDefinitionRevision, schema.FlatteningMap, codebook, schema.Locales);
}
