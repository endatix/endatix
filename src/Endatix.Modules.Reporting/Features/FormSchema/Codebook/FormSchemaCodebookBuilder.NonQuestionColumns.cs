using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>Codebook entries for columns no question owns: calculated values and audience values.</summary>
internal static partial class FormSchemaCodebookBuilder
{
    /// <summary>What sets a non-question entry apart; the export shape follows the column's kind.</summary>
    private sealed record NonQuestionEntry(string SurveyJsType, string Title, string? DataType = null);

    // A calculated value is not a question, even when one shares its name: it never takes a
    // question's type, shape or title.
    private static JsonElement CreateCalculatedColumnEntry(FormSchemaColumn column) =>
        CreateNonQuestionColumnEntry(
            column,
            new NonQuestionEntry(FormSchemaCodebookPropertyNames.CalculatedValueSurveyJsType, column.Key));

    // An audience value is not a question either; its data type is the audience property's, as frozen.
    private static JsonElement CreatePersonalizationColumnEntry(FormSchemaColumn column) =>
        CreateNonQuestionColumnEntry(
            column,
            new NonQuestionEntry(FormSchemaCodebookPropertyNames.PersonalizationSurveyJsType, column.Label, column.DataType));

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
