using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// What an export asks of its column plan. <c>ReadWithShojiCodebook</c>: the export is read with the Shoji codebook,
/// which a column scope does not narrow, so a question outside the scope still takes its export key from audience and
/// submitter profile columns.
/// </summary>
internal sealed record ExportColumnPlanRequest(
    string Locale = FormSchemaCodebookPropertyNames.Default,
    ColumnAliasProfile AliasProfile = ColumnAliasProfile.Native,
    IReadOnlySet<string>? ColumnScope = null,
    string KeySeparator = ExportFormatSettings.DefaultKeySeparator,
    IColumnAliasTransformerRegistry? AliasRegistry = null,
    bool ReadWithShojiCodebook = false);
