namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// JSON property names for persisted form-schema codebook metadata.
/// Shared flattening-map keys live in <see cref="FormSchema.FormSchemaPropertyNames"/>.
/// SurveyJS definition keys live in <see cref="Shared.SurveyJs.SurveyJsPropertyNames"/>.
/// </summary>
internal static class FormSchemaCodebookPropertyNames
{
    public const string Version = "version";
    public const string Locales = "locales";
    public const string Questions = "questions";
    public const string Columns = "columns";
    public const string ChoiceCatalogs = "choiceCatalogs";

    public const string ParentKey = "parentKey";
    public const string ParentPanelTitle = "parentPanelTitle";
    public const string SurveyJsType = "surveyJsType";
    public const string ExportShape = "exportShape";
    public const string ChoiceLabel = "choiceLabel";
    public const string RowLabel = "rowLabel";
    public const string ColumnLabel = "columnLabel";

    public const string Id = "id";
    public const string Default = "default";

    public const string UnknownSurveyJsType = "unknown";
}
