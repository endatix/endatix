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
    public const string NUMERIC_CODE = "numericCode";

    /// <summary>Choice catalog: the special item a choice is ("other", "none", "refused", "dontknow"); absent for an inline choice.</summary>
    public const string Kind = "kind";

    /// <summary>Choice catalog: true when choosing the item clears every other choice.</summary>
    public const string Exclusive = "exclusive";

    /// <summary>Choice catalog: present on a base select; true when it stores several values.</summary>
    public const string Multiple = "multiple";

    /// <summary>Choice catalog: where the Other text is stored, "comment" or "value"; only with an Other item.</summary>
    public const string OtherStorage = "otherStorage";

    /// <summary>
    /// Choice catalog of a copy that offers its source's Other: the source's name. The copy stores "other" but no text;
    /// the text is the source's.
    /// </summary>
    public const string OtherFrom = "otherFrom";

    /// <summary>
    /// Question: where a single select's choices come from, when not its own <c>choices</c>: <c>kind</c> ("dataList",
    /// "carryForward", "unresolvable") and <c>listId</c>, <c>sources</c> or <c>reason</c>. Absent means inline.
    /// </summary>
    public const string ChoiceSource = "choiceSource";
    public const string ListId = "listId";
    public const string Sources = "sources";
    public const string Reason = "reason";

    /// <summary>
    /// Choice catalog of a copied checkbox or tagbox: <c>mode</c> and <c>sources</c>, present only when the choices a
    /// respondent was shown follow from the sources' answers alone. Absent means unknown.
    /// </summary>
    public const string Shown = "shown";

    /// <summary>
    /// Choice catalog: true once any compiled definition made the select a copy. Kept by every merge, so rows saved
    /// while it copied never read as a select with its own choices.
    /// </summary>
    public const string Copied = "copied";

    /// <summary>
    /// Choice catalog of a checkbox or tagbox with its own choices: true once any compiled definition put it under a
    /// <c>visibleIf</c> (its own or an enclosing panel's, dynamic panel's or page's) or a static <c>visible: false</c>.
    /// Kept by every merge, so an unanswered row saved under such a definition never claims it was shown.
    /// </summary>
    public const string CanBeHidden = "canBeHidden";
    public const string Mode = "mode";

    /// <summary><see cref="Shown"/>: a source's name and the regular choice values it offered when compiled.</summary>
    public const string SourceName = "name";
    public const string Values = "values";

    /// <summary><see cref="Shown"/>: the rule holds only for a completed submission (a hidden answer is cleared then).</summary>
    public const string WhenComplete = "whenComplete";

    /// <summary>Question: "text" when a single select's choices cannot be read, so its answers export as text.</summary>
    public const string Degraded = "degraded";
    public const string DegradedToText = "text";

    /// <summary>The participants block an export adds: its title, identifier column and variable columns.</summary>
    public const string Participants = "participants";
    public const string Identifier = "identifier";
    public const string Variables = "variables";

    /// <inheritdoc cref="Domain.SurveyJs.SurveyJsElementType.DefaultLocale"/>
    public const string Default = Domain.SurveyJs.SurveyJsElementType.DefaultLocale;

    public const string UnknownSurveyJsType = "unknown";

    /// <summary>SurveyJS class name of a calculated value (custom variable).</summary>
    public const string CalculatedValueSurveyJsType = "calculatedvalue";

    /// <summary>Not a SurveyJS class: a participant value frozen on the response, which a survey reads as a variable.</summary>
    public const string ParticipantSurveyJsType = "participant";

    /// <summary>Not a SurveyJS class: a submitter profile field captured with the response.</summary>
    public const string SubmitterProfileSurveyJsType = "submitter_profile";
}
