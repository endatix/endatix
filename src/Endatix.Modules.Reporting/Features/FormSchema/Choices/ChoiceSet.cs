using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using Storage = Endatix.Modules.Reporting.Features.FormSchema.Choices.OtherStorage;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One storable item of a select question, in catalog order.</summary>
/// <param name="Value">The value SurveyJS stores.</param>
/// <param name="Kind">An inline choice or a special item.</param>
/// <param name="Exclusive">Whether choosing it clears every other choice.</param>
/// <param name="FallbackLabel">The label when <paramref name="LabelSource"/> has none.</param>
/// <param name="LabelSource">The choice element, or the question for a special item.</param>
/// <param name="LabelProperty">The localized label property on <paramref name="LabelSource"/>.</param>
/// <param name="Copied">A choice copied from another question, whose element is its label source.</param>
internal sealed record ChoiceSetItem(
    string Value,
    ChoiceKind Kind,
    bool Exclusive,
    string FallbackLabel,
    JsonElement LabelSource,
    string LabelProperty,
    bool Copied = false);

/// <summary>
/// COMPILE TIME. The values a checkbox, tagbox, radiogroup or dropdown (and the other base selects) can store, read
/// from its definition. The only reader of the special item flags, <c>storeOthersAsComment</c>,
/// <c>showCommentArea</c> and <c>isExclusive</c>.
/// </summary>
/// <remarks>
/// <see cref="Items"/> are the inline choices in definition order (or, given <see cref="ResolvedChoices"/>, the choices
/// they resolve to), then Other, None, Refused and Don't know
/// (<see cref="SpecialChoices.All"/>), then a source's Other the copy offers (<see cref="ResolvedChoices.Other"/>), last
/// so that it was added after every item a codebook may already hold. Positional ids never shift for existing items.
/// Select All is never an item, because it is never stored.
/// </remarks>
internal sealed class ChoiceSet
{
    private ChoiceSet()
    {
    }

    public IReadOnlyList<ChoiceSetItem> Items { get; private init; } = [];

    public bool IsMultiple { get; private init; }

    /// <summary>Whether the question shows an Other item.</summary>
    public bool HasOther => Items.Any(item => item.Kind == ChoiceKind.Other);

    /// <summary>A readable choice list, including a copy whose only choice is its source's Other.</summary>
    public bool HasIndicatorChoices => OtherFrom is not null || Items.Any(item => item.Kind == ChoiceKind.Choice);

    /// <summary>Null when the question shows no Other item.</summary>
    public OtherStorage? OtherStorage { get; private init; }

    /// <summary>The source whose Other the copy offers, where that Other's text is stored; null otherwise.</summary>
    public string? OtherFrom { get; private init; }

    /// <summary>
    /// Inline choices and the question's own Other only: the values a <c>loopSource</c> driver turns into panels. A
    /// copied Other is left out: whether Hub gives it a panel is not known yet.
    /// </summary>
    public IEnumerable<string> LoopDriverValues =>
        Items.Where(item => item.Kind == ChoiceKind.Choice || item is { Kind: ChoiceKind.Other, Copied: false })
            .Select(item => item.Value);

    /// <summary>Null for a question that is not a base select (ranking included).</summary>
    /// <param name="question">The question's definition element.</param>
    /// <param name="survey">The survey-level defaults; <see cref="SurveyChoiceDefaults.Default"/> when null.</param>
    /// <param name="choices">Its resolved choices (<see cref="ChoiceSources"/>); its inline choices when null.</param>
    public static ChoiceSet? Read(JsonElement question, SurveyChoiceDefaults? survey = null, ResolvedChoices? choices = null)
    {
        var multiple = question.IsMultiSelectBaseSelect();
        if (!multiple && !question.IsSingleSelectBaseSelect())
        {
            return null;
        }

        var items = ChoiceItems(question, choices);
        items.AddRange(SpecialItems(question, items));
        var otherFrom = AddCopiedOther(items, choices?.Other);
        return new ChoiceSet
        {
            Items = items,
            IsMultiple = multiple,
            OtherFrom = otherFrom,
            OtherStorage = OtherStorageOf(question, items, survey),
        };
    }

    private static OtherStorage? OtherStorageOf(JsonElement question, List<ChoiceSetItem> items, SurveyChoiceDefaults? survey) =>
        items.Exists(item => item.Kind == ChoiceKind.Other)
            ? ResolveOtherStorage(question, survey ?? SurveyChoiceDefaults.Default)
            : null;

    // Adds the source's Other last; the source's name when it was added.
    private static string? AddCopiedOther(List<ChoiceSetItem> items, CopiedOther? other)
    {
        if (other is not { } copied || CopiedOtherItem(copied.Source, items) is not { } item)
        {
            return null;
        }

        items.Add(item);
        return copied.SourceName;
    }

    private static List<ChoiceSetItem> ChoiceItems(JsonElement question, ResolvedChoices? resolved)
    {
        var choices = resolved is null
            ? SurveyJsChoiceHelper.EnumerateChoiceElements(question)
                .Select(choice => new SourceChoice(choice.Value, choice.Text, choice.Element))
            : resolved.Choices;
        var copied = resolved?.IsCopied == true;
        return
        [
            .. choices.Select(choice => new ChoiceSetItem(
                choice.Value,
                ChoiceKind.Choice,
                choice.Element.ValueKind == JsonValueKind.Object &&
                choice.Element.GetBooleanProperty(SurveyJsPropertyNames.IsExclusive),
                choice.Text,
                choice.Element,
                SurveyJsPropertyNames.Text,
                copied)),
        ];
    }

    // An inline choice that already stores a special value keeps it; the special item is not added twice.
    private static List<ChoiceSetItem> SpecialItems(JsonElement question, List<ChoiceSetItem> inline)
    {
        var supportsAllSpecials = SupportsAllSpecialItems(question);
        return SpecialChoices.All
            .Where(spec => (spec.AllTypes || supportsAllSpecials) &&
                           Shows(question, spec) &&
                           inline.TrueForAll(item => !string.Equals(item.Value, spec.Value, StringComparison.Ordinal)))
            .Select(spec => new ChoiceSetItem(
                spec.Value,
                spec.Kind,
                spec.Exclusive,
                question.GetStringProperty(spec.TextProperty) ?? spec.DefaultText,
                question,
                spec.TextProperty))
            .ToList();
    }

    // The source's Other, labelled with the source's otherText; not when a copied choice already stores "other".
    private static ChoiceSetItem? CopiedOtherItem(JsonElement source, List<ChoiceSetItem> items)
    {
        if (items.Exists(item => string.Equals(item.Value, SurveyJsPropertyNames.Other, StringComparison.Ordinal)))
        {
            return null;
        }

        var spec = SpecialChoices.Other;
        return new ChoiceSetItem(
            spec.Value,
            spec.Kind,
            spec.Exclusive,
            source.GetStringProperty(spec.TextProperty) ?? spec.DefaultText,
            source,
            spec.TextProperty,
            Copied: true);
    }

    /// <summary>Whether the question shows its own Other item (<c>showOtherItem</c>, or the older <c>hasOther</c>).</summary>
    public static bool ShowsOther(JsonElement question) => Shows(question, SpecialChoices.Other);

    /// <summary>
    /// Writes the catalog: <c>multiple</c>, <c>otherStorage</c>, <c>otherFrom</c>, then <c>choices</c> with id =
    /// position + 1.
    /// </summary>
    public void WriteCatalog(Utf8JsonWriter writer, Action<ChoiceSetItem, int> writeChoice)
    {
        writer.WriteBoolean(FormSchemaCodebookPropertyNames.Multiple, IsMultiple);
        if (OtherStorage is { } storage)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.OtherStorage, ToJsonName(storage));
        }

        if (OtherFrom is { } source)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.OtherFrom, source);
        }

        writer.WritePropertyName(SurveyJsPropertyNames.Choices);
        writer.WriteStartArray();
        WriteChoices(writeChoice);
        writer.WriteEndArray();
    }

    /// <summary>Calls <paramref name="writeChoice"/> for each item with its positional id (position + 1).</summary>
    public void WriteChoices(Action<ChoiceSetItem, int> writeChoice)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            writeChoice(Items[index], index + 1);
        }
    }

    /// <summary>The item with this stored value, if any.</summary>
    public ChoiceSetItem? Find(string value) =>
        Items.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal));

    internal static string ToJsonName(OtherStorage storage) => storage == Storage.Value ? "value" : "comment";

    internal static OtherStorage? ParseOtherStorage(string? name) =>
        name switch
        {
            "value" => Storage.Value,
            "comment" => Storage.Comment,
            _ => null,
        };

    // survey-core question_baseselect.ts getStoreOthersAsComment: the question comment owns "-Comment", per-choice
    // comments force comment storage, then the question's own setting, then the survey's; choicesByUrl forces it.
    // question_checkbox.ts overrides it: a checkbox or tag box whose items are objects keeps the typed text in the
    // value. That is read only on the question's own inline choices, where typed text can be told apart.
    private static OtherStorage ResolveOtherStorage(JsonElement question, SurveyChoiceDefaults survey)
    {
        if (question.GetBooleanProperty(SurveyJsPropertyNames.ShowCommentArea) ||
            question.GetBooleanProperty(SurveyJsPropertyNames.HasComment))
        {
            return Storage.Value;
        }

        if (question.TryGetProperty(SurveyJsPropertyNames.ChoicesByUrl, out _))
        {
            return Storage.Comment;
        }

        // Typed text in the value can only be told apart from a choice when every choice is on the form. Choices
        // copied from another question, loaded later or added by the respondent are not, so record comment storage:
        // the value stays as stored.
        if (!HasOwnChoices(question))
        {
            return Storage.Comment;
        }

        if (StoresObjectItems(question))
        {
            return Storage.Value;
        }

        return ShowsChoiceComments(question) || StoresOthersAsComment(question, survey) ? Storage.Comment : Storage.Value;
    }

    private static bool StoresOthersAsComment(JsonElement question, SurveyChoiceDefaults survey) =>
        question.TryGetProperty(SurveyJsPropertyNames.StoreOthersAsComment, out var own) &&
        own.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? own.ValueKind == JsonValueKind.True
            : survey.StoreOthersAsComment;

    // Leftover inline choices on a copy or a data list are not what the respondent sees, so only an inline source counts.
    private static bool HasOwnChoices(JsonElement question) =>
        SurveyJsChoiceHelper.EnumerateChoiceElements(question).Any() &&
        ChoiceSource.Parse(question) is ChoiceSource.Inline &&
        !question.GetBooleanProperty(SurveyJsPropertyNames.AllowCustomChoices);

    // question_checkbox.ts getValuePropertyName: a checkbox or a tag box with a valuePropertyName, or a checkbox whose
    // choice shows a comment box, stores each checked item as an object.
    private static bool StoresObjectItems(JsonElement question)
    {
        var type = question.GetSurveyJsType();
        var isCheckbox = SurveyJsElementType.Checkbox.Matches(type);
        if (!isCheckbox && !SurveyJsElementType.Tagbox.Matches(type))
        {
            return false;
        }

        return !string.IsNullOrEmpty(question.GetStringProperty(SurveyJsPropertyNames.ValuePropertyName)) ||
               (isCheckbox && ShowsChoiceComments(question));
    }

    // A tag box's choices show no comment box, so survey-core ignores the setting there.
    private static bool ShowsChoiceComments(JsonElement question) =>
        !SurveyJsElementType.Tagbox.Matches(question.GetSurveyJsType()) &&
        SurveyJsChoiceHelper.EnumerateChoiceElements(question).Any(choice =>
            choice.Element.ValueKind == JsonValueKind.Object &&
            choice.Element.GetBooleanProperty(SurveyJsPropertyNames.ShowCommentArea));

    private static bool Shows(JsonElement question, SpecialChoiceSpec spec) =>
        question.GetBooleanProperty(spec.ShowProperty) ||
        (spec.AliasProperty is not null && question.GetBooleanProperty(spec.AliasProperty));

    // survey-core: ranking and imagepicker turn None, Refuse and Don't know off; buttongroup has no special items.
    private static bool SupportsAllSpecialItems(JsonElement question)
    {
        var type = question.GetSurveyJsType();
        return SurveyJsElementType.Checkbox.Matches(type) ||
               SurveyJsElementType.Tagbox.Matches(type) ||
               SurveyJsElementType.Radiogroup.Matches(type) ||
               SurveyJsElementType.Dropdown.Matches(type);
    }
}
