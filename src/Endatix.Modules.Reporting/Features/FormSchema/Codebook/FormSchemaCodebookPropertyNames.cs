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
    public const string ParentPanels = "parentPanels";
    public const string PanelKey = "key";
    public const string PanelTitle = "title";
    public const string PageName = "pageName";
    public const string PageTitle = "pageTitle";
    public const string SurveyJsType = "surveyJsType";
    public const string ExportShape = "exportShape";
    public const string ChoiceLabel = "choiceLabel";
    public const string RowLabel = "rowLabel";
    public const string ColumnLabel = "columnLabel";

    public const string Id = "id";

    /// <summary>Choice catalog: the special item a choice is ("other", "none", "refused", "dontknow"); absent for an inline choice.</summary>
    public const string Kind = "kind";

    /// <summary>Choice catalog: true when choosing the item clears every other choice.</summary>
    public const string Exclusive = "exclusive";

    /// <summary>Choice catalog: present on a base select; true when it stores several values.</summary>
    public const string Multiple = "multiple";

    /// <summary>Choice catalog: where the Other text is stored, "comment" or "value"; only with an Other item.</summary>
    public const string OtherStorage = "otherStorage";

    /// <summary>The audience block an export adds: its title, identifier column and variable columns.</summary>
    public const string Personalization = "personalization";
    public const string Identifier = "identifier";
    public const string Variables = "variables";

    /// <inheritdoc cref="Domain.SurveyJs.SurveyJsElementType.DefaultLocale"/>
    public const string Default = Domain.SurveyJs.SurveyJsElementType.DefaultLocale;

    public const string UnknownSurveyJsType = "unknown";

    /// <summary>SurveyJS class name of a calculated value (custom variable).</summary>
    public const string CalculatedValueSurveyJsType = "calculatedvalue";

    /// <summary>Not a SurveyJS class: an audience value frozen on the response, which a survey reads as a variable.</summary>
    public const string PersonalizationSurveyJsType = "personalization";

    /// <summary>Not a SurveyJS class: a submitter profile field captured with the response.</summary>
    public const string SubmitterProfileSurveyJsType = "submitter_profile";
}
