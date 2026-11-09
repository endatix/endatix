using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// One SurveyJS special item: the value it stores, the properties that show and label it, and how Crunch treats it.
/// </summary>
/// <param name="Kind">The item.</param>
/// <param name="Value">The stored value (SurveyJS <c>settings.noneItemValue</c> and its siblings).</param>
/// <param name="ShowProperty">The question property that shows the item.</param>
/// <param name="AliasProperty">The older name of <paramref name="ShowProperty"/>, if any.</param>
/// <param name="TextProperty">The question property that labels the item.</param>
/// <param name="DefaultText">The label when <paramref name="TextProperty"/> is not set.</param>
/// <param name="Exclusive">Whether choosing the item clears every other choice.</param>
/// <param name="MissingInCrunch">Whether Crunch writes the item as a missing category (a non-substantive answer).</param>
/// <param name="AllTypes">
/// Whether every base select supports the item. The others are for checkbox, tagbox, radiogroup and dropdown only.
/// </param>
internal sealed record SpecialChoiceSpec(
    ChoiceKind Kind,
    string Value,
    string ShowProperty,
    string? AliasProperty,
    string TextProperty,
    string DefaultText,
    bool Exclusive,
    bool MissingInCrunch,
    bool AllTypes);

/// <summary>
/// The SurveyJS special items a select question can store, in catalog order. Other comes first so the positional ids
/// that existing datasets already hold never shift; Select All is not here because it is never stored.
/// </summary>
internal static class SpecialChoices
{
    public static IReadOnlyList<SpecialChoiceSpec> All { get; } =
    [
        new(ChoiceKind.Other, SurveyJsPropertyNames.Other, SurveyJsPropertyNames.ShowOtherItem, SurveyJsPropertyNames.HasOther,
            SurveyJsPropertyNames.OtherText, "Other", Exclusive: false, MissingInCrunch: false, AllTypes: true),
        new(ChoiceKind.None, "none", SurveyJsPropertyNames.ShowNoneItem, SurveyJsPropertyNames.HasNone,
            SurveyJsPropertyNames.NoneText, "None", Exclusive: true, MissingInCrunch: false, AllTypes: false),
        new(ChoiceKind.Refused, "refused", SurveyJsPropertyNames.ShowRefuseItem, AliasProperty: null,
            SurveyJsPropertyNames.RefuseText, "Refuse to answer", Exclusive: true, MissingInCrunch: true, AllTypes: false),
        new(ChoiceKind.DontKnow, "dontknow", SurveyJsPropertyNames.ShowDontKnowItem, AliasProperty: null,
            SurveyJsPropertyNames.DontKnowText, "Don't know", Exclusive: true, MissingInCrunch: true, AllTypes: false),
    ];

    /// <summary>The persisted <c>kind</c>: the special item's value; null for an inline choice.</summary>
    public static string? JsonName(this ChoiceKind kind) => Of(kind)?.Value;

    public static ChoiceKind ParseJsonName(string? name) =>
        All.FirstOrDefault(spec => string.Equals(spec.Value, name, StringComparison.Ordinal))?.Kind ?? ChoiceKind.Choice;

    /// <summary>A non-substantive answer (Refused, Don't know). Crunch writes these as missing categories.</summary>
    public static bool IsNonResponse(this ChoiceKind kind) => Of(kind)?.MissingInCrunch ?? false;

    private static SpecialChoiceSpec? Of(ChoiceKind kind) => All.FirstOrDefault(spec => spec.Kind == kind);
}
