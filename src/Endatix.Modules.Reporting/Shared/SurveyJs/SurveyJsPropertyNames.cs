namespace Endatix.Modules.Reporting.Shared.SurveyJs;

/// <summary>
/// JSON property names used in SurveyJS form definitions. Persisted form schema JSON uses
/// <c>FormSchemaPropertyNames</c> instead.
/// </summary>
internal static class SurveyJsPropertyNames
{
    public const string Type = "type";
    public const string Name = "name";
    public const string Title = "title";
    public const string Description = "description";
    public const string Text = "text";
    public const string Value = "value";
    public const string ValueName = "valueName";
    public const string ValuePropertyName = "valuePropertyName";
    public const string Elements = "elements";
    public const string TemplateElements = "templateElements";
    public const string LoopSource = "loopSource";
    public const string ItemValue = "itemValue";
    public const string Pages = "pages";
    public const string Choices = "choices";
    public const string Rows = "rows";
    public const string Columns = "columns";
    public const string Items = "items";
    public const string CalculatedValues = "calculatedValues";
    public const string IncludeIntoResult = "includeIntoResult";
    public const string InputType = "inputType";
    public const string InputTypeDate = "date";
    public const string InputTypeDateTime = "datetime";
    public const string InputTypeDateTimeLocal = "datetime-local";
    public const string ShowOtherItem = "showOtherItem";
    public const string OtherText = "otherText";
    public const string HasOther = "hasOther";
    public const string ShowNoneItem = "showNoneItem";
    public const string HasNone = "hasNone";
    public const string NoneText = "noneText";
    public const string ShowRefuseItem = "showRefuseItem";
    public const string RefuseText = "refuseText";
    public const string ShowDontKnowItem = "showDontKnowItem";
    public const string DontKnowText = "dontKnowText";
    public const string StoreOthersAsComment = "storeOthersAsComment";
    public const string ShowCommentArea = "showCommentArea";
    public const string HasComment = "hasComment";
    public const string IsExclusive = "isExclusive";
    public const string ChoicesByUrl = "choicesByUrl";
    public const string ChoicesFromQuestion = "choicesFromQuestion";

    /// <summary>SurveyJS: which of the source's choices a <see cref="ChoicesFromQuestion"/> copy shows (all, selected, unselected).</summary>
    public const string ChoicesFromQuestionMode = "choicesFromQuestionMode";
    public const string ChoicesVisibleIf = "choicesVisibleIf";
    public const string Visible = "visible";
    public const string VisibleIf = "visibleIf";
    public const string ChoiceValuesFromQuestion = "choiceValuesFromQuestion";
    public const string AllowCustomChoices = "allowCustomChoices";

    /// <summary>Hub: the data list a select question loads its choices from.</summary>
    public const string DataListId = "edxDataListId";

    /// <summary>Hub carry forward: on when <c>true</c>, copying the choices of <see cref="CarryForwardSources"/>.</summary>
    public const string CarryForwardEnabled = "edxCarryForwardEnabled";

    /// <summary>Hub carry forward: the names of the questions whose choices are copied, in order.</summary>
    public const string CarryForwardSources = "edxCarryForwardSources";

    /// <summary>Hub carry forward: which of the sources' choices are copied (all, selected, unselected).</summary>
    public const string CarryForwardMode = "edxCarryForwardMode";

    /// <summary>Hub carry forward: at most this many copied choices are shown; 0 or absent is no cap.</summary>
    public const string CarryForwardMaxChoices = "edxCarryForwardMaxChoices";
    public const string Url = "url";
    public const string Comment = "comment";

    /// <summary>SurveyJS <c>settings.commentSuffix</c>: the key of a question's Other text or comment.</summary>
    public const string CommentSuffix = "-Comment";
    public const string LabelTrue = "labelTrue";
    public const string LabelFalse = "labelFalse";
    public const string MaxRowCount = "maxRowCount";
    public const string RowCount = "rowCount";
    public const string MaxPanelCount = "maxPanelCount";
    public const string Content = "content";
    public const string Other = "other";
    public const string CellType = "cellType";
    public const string DefaultCellType = "defaultCellType";
    public const string MultiSelect = "multiSelect";
    public const string SliderType = "sliderType";
    public const string SliderTypeRange = "range";

    /// <inheritdoc cref="Domain.SurveyJs.SurveyJsElementType.DefaultLocale"/>
    public const string DefaultLocale = Domain.SurveyJs.SurveyJsElementType.DefaultLocale;
}
