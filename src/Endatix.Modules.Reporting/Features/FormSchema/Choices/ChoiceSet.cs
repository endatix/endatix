using System.Text.Json;
using Storage = Endatix.Modules.Reporting.Features.FormSchema.Choices.OtherStorage;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One storable item of a select question, in catalog order.</summary>
/// <param name="Value">The value SurveyJS stores.</param>
/// <param name="Kind">An inline choice or a special item.</param>
/// <param name="Exclusive">Whether choosing it clears every other choice.</param>
/// <param name="FallbackLabel">The label when <paramref name="LabelSource"/> has none.</param>
/// <param name="LabelSource">The choice element, or the question for a special item.</param>
/// <param name="LabelProperty">The localized label property on <paramref name="LabelSource"/>.</param>
internal sealed record ChoiceSetItem(
    string Value,
    ChoiceKind Kind,
    bool Exclusive,
    string FallbackLabel,
    JsonElement LabelSource,
    string LabelProperty);

/// <summary>
/// COMPILE TIME. The values a checkbox, tagbox, radiogroup or dropdown (and the other base selects) can store, read
/// from its definition. The only reader of the special item flags, <c>storeOthersAsComment</c>,
/// <c>showCommentArea</c> and <c>isExclusive</c>.
/// </summary>
/// <remarks>
/// <see cref="Items"/> are the inline choices in definition order, then Other, None, Refused and Don't know
/// (<see cref="SpecialChoices.All"/>), so positional ids never shift for existing items. Select All is never an item,
/// because it is never stored.
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

    /// <summary>Whether the question has inline choices; a lazy multi-select (data list) has none.</summary>
    public bool HasInlineChoices => Items.Any(item => item.Kind == ChoiceKind.Choice);

    /// <summary>Null when the question shows no Other item.</summary>
    public OtherStorage? OtherStorage { get; private init; }

    /// <summary>Inline choices and Other only: the values a <c>loopSource</c> driver turns into panels.</summary>
    public IEnumerable<string> LoopDriverValues =>
        Items.Where(item => item.Kind is ChoiceKind.Choice or ChoiceKind.Other).Select(item => item.Value);

    /// <summary>Null for a question that is not a base select (ranking included).</summary>
    public static ChoiceSet? Read(JsonElement question, SurveyChoiceDefaults? survey = null)
    {
        var multiple = question.IsMultiSelectBaseSelect();
        if (!multiple && !question.IsSingleSelectBaseSelect())
        {
            return null;
        }

        var items = InlineItems(question);
        items.AddRange(SpecialItems(question, items));
        return new ChoiceSet
        {
            Items = items,
            IsMultiple = multiple,
            OtherStorage = items.Exists(item => item.Kind == ChoiceKind.Other)
                ? ResolveOtherStorage(question, survey ?? SurveyChoiceDefaults.Default)
                : null,
        };
    }

    private static List<ChoiceSetItem> InlineItems(JsonElement question) =>
    [
        .. SurveyJsChoiceHelper.EnumerateChoiceElements(question).Select(choice => new ChoiceSetItem(
            choice.Value,
            ChoiceKind.Choice,
            choice.Element.ValueKind == JsonValueKind.Object &&
            choice.Element.GetBooleanProperty(SurveyJsPropertyNames.IsExclusive),
            choice.Text,
            choice.Element,
            SurveyJsPropertyNames.Text)),
    ];

    // An inline choice that already stores a special value keeps it; the special item is not added twice.
    private static IEnumerable<ChoiceSetItem> SpecialItems(JsonElement question, List<ChoiceSetItem> inline)
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

    /// <summary>Writes the catalog: <c>multiple</c>, <c>otherStorage</c>, then <c>choices</c> with id = position + 1.</summary>
    public void WriteCatalog(Utf8JsonWriter writer, Action<ChoiceSetItem, int> writeChoice)
    {
        writer.WriteBoolean(FormSchemaCodebookPropertyNames.Multiple, IsMultiple);
        if (OtherStorage is { } storage)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.OtherStorage, ToJsonName(storage));
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
    private static OtherStorage ResolveOtherStorage(JsonElement question, SurveyChoiceDefaults survey)
    {
        if (question.GetBooleanProperty(SurveyJsPropertyNames.ShowCommentArea) ||
            question.GetBooleanProperty(SurveyJsPropertyNames.HasComment))
        {
            return Storage.Value;
        }

        if (HasChoiceComments(question) || question.TryGetProperty(SurveyJsPropertyNames.ChoicesByUrl, out _))
        {
            return Storage.Comment;
        }

        var storesComment = question.TryGetProperty(SurveyJsPropertyNames.StoreOthersAsComment, out var own) &&
                            own.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? own.ValueKind == JsonValueKind.True
            : survey.StoreOthersAsComment;

        return storesComment ? Storage.Comment : Storage.Value;
    }

    private static bool HasChoiceComments(JsonElement question) =>
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
