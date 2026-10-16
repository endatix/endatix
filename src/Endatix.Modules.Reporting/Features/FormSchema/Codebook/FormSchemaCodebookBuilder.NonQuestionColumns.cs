using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Codebook entries for columns no question owns: calculated values, audience values and submitter profile fields.
/// </summary>
internal static partial class FormSchemaCodebookBuilder
{
    /// <summary>What sets a non-question entry apart; the export shape follows the column's kind.</summary>
    private sealed record NonQuestionEntry(string SurveyJsType, string Title, string? DataType = null);

    private static JsonElement CreateNonQuestionColumnEntry(FormSchemaColumn column) =>
        CreateNonQuestionColumnEntry(column, NonQuestionEntryOf(column));

    // A calculated value is not a question, even when one shares its name: it never takes a question's type, shape or
    // title. An audience value is not a question either; its data type is the audience property's, as frozen. Nor is
    // a submitter profile field, whose value is the snapshot's text.
    private static NonQuestionEntry NonQuestionEntryOf(FormSchemaColumn column) =>
        column.Kind switch
        {
            FormSchemaColumnKind.Calculated =>
                new(FormSchemaCodebookPropertyNames.CalculatedValueSurveyJsType, column.Key),
            FormSchemaColumnKind.Personalization =>
                new(FormSchemaCodebookPropertyNames.PersonalizationSurveyJsType, column.Label, column.DataType),
            FormSchemaColumnKind.SubmitterProfile =>
                new(FormSchemaCodebookPropertyNames.SubmitterProfileSurveyJsType, column.Label, column.DataType),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Kind, "A question owns this column."),
        };

    private static JsonElement CreateNonQuestionColumnEntry(FormSchemaColumn column, NonQuestionEntry entry) =>
        WriteColumnEntry(writer =>
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.ParentKey, column.Key);
            writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, entry.SurveyJsType);
            writer.WriteString(FormSchemaCodebookPropertyNames.ExportShape, FormSchemaCodebookExportShape.FromColumnKind(column.Kind).Name);
            if (entry.DataType is not null)
            {
                writer.WriteString(FormSchemaPropertyNames.DataType, entry.DataType);
            }

            writer.WritePropertyName(SurveyJsPropertyNames.Title);
            SurveyJsLocalizationHelper.WriteLocalizedStrings(
                writer,
                new Dictionary<string, string> { [FormSchemaCodebookPropertyNames.Default] = entry.Title });
        });
}
