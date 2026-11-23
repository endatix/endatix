using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Microsoft.Extensions.Logging;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// The form schema an export runs with: as stored, except that its codebook names and orders the audience block as
/// the Audience tab is now. A rename or reorder never touches the stored schema.
/// </summary>
internal sealed partial class ExportSchemas(
    IFormSchemaRepository schemas,
    IAudiencePropertyCatalog catalog,
    ILogger<ExportSchemas> logger)
{
    private const string CodebookMetadataKey = "ReportingExportCodebook";

    public Task<FormSchemaEntity?> GetAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        schemas.GetByFormIdAsync(tenantId, formId, cancellationToken);

    /// <summary>One catalog read per call, and none for a form without audience columns.</summary>
    public async Task<Result<string>> CodebookAsync(FormSchemaEntity schema, SpecialChoiceCodes? codes, CancellationToken cancellationToken)
    {
        var json = await AudienceCodebookAsync(schema, cancellationToken);
        return codes is null ? json : new ChoiceCodes(codes).Apply(json);
    }

    private async Task<string> AudienceCodebookAsync(FormSchemaEntity schema, CancellationToken cancellationToken)
    {
        WarnQuestionsExportedAsText(schema);
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
    public async Task<Result<ExportOptions>> PrepareCodebookAsync(FormSchemaEntity schema, ExportOptions options, CancellationToken cancellationToken)
    {
        var settings = options.Metadata?.TryGetValue(SubmissionExportMetadataKeys.ResolvedFormatSettings, out var value) == true
            ? value as ExportFormatSettings : null;
        var codebook = await CodebookAsync(schema, settings?.SpecialChoiceCodes, cancellationToken);
        if (!codebook.IsSuccess)
        {
            return codebook.ToErrorResult<ExportOptions>();
        }

        options.Metadata ??= new Dictionary<string, object>();
        options.Metadata[CodebookMetadataKey] = codebook.Value;
        return options;
    }

    /// <summary>The codebook <see cref="PrepareCodebookAsync"/> kept; read again when the export was not prepared.</summary>
    public async Task<string> PreparedCodebookAsync(
        FormSchemaEntity schema,
        ExportOptions options,
        CancellationToken cancellationToken) =>
        options.Metadata?.TryGetValue(CodebookMetadataKey, out var prepared) == true && prepared is string codebook
            ? codebook
            : await AudienceCodebookAsync(schema, cancellationToken);

    /// <summary>A detached copy of <paramref name="schema"/> with <paramref name="codebook"/>; never saved.</summary>
    public static FormSchemaEntity WithCodebook(FormSchemaEntity schema, string codebook) =>
        new(schema.TenantId, schema.FormId, schema.FormDefinitionRevision, schema.FlatteningMap, codebook, schema.Locales);

    // Every export reads the codebook once through here, so a form whose copies cannot be read warns once per export.
    private void WarnQuestionsExportedAsText(FormSchemaEntity schema)
    {
        var questions = CodebookQuestions.DegradedToText(schema.Codebook);
        if (questions.Count > 0)
        {
            LogQuestionsExportedAsText(schema.FormId, string.Join(", ", questions));
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Export of form {FormId}: these single-select questions have no codebook categories, so exports write them as text, because they copy choices from a source the codebook cannot read (a data list, a URL, answers or a cycle) or from none: {Questions}")]
    private partial void LogQuestionsExportedAsText(long formId, string questions);
}
