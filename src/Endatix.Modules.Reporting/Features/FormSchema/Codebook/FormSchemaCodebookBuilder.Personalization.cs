using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>Codebook entries for the audience values frozen on responses.</summary>
internal static partial class FormSchemaCodebookBuilder
{
    // An audience value is not a question either; its data type is the audience property's, as frozen.
    private static JsonElement CreatePersonalizationColumnEntry(FormSchemaColumn column) =>
        WriteColumnEntry(writer =>
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.ParentKey, column.Key);
            writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, FormSchemaCodebookPropertyNames.PersonalizationSurveyJsType);
            writer.WriteString(FormSchemaCodebookPropertyNames.ExportShape, FormSchemaCodebookExportShape.Personalization.Name);
            writer.WriteString(FormSchemaPropertyNames.DataType, column.DataType);
            writer.WritePropertyName(SurveyJsPropertyNames.Title);
            SurveyJsLocalizationHelper.WriteLocalizedStrings(
                writer,
                new Dictionary<string, string> { [FormSchemaCodebookPropertyNames.Default] = column.Label });
        });
}
