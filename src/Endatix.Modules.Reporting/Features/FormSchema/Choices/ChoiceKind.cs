namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>What a stored choice value means. Persisted as <c>kind</c> on choice catalog entries; omitted for <see cref="Choice"/>.</summary>
internal enum ChoiceKind
{
    /// <summary>An inline choice from the question's <c>choices</c>.</summary>
    Choice,
    Other,
    None,
    Refused,
    DontKnow,
}

/// <summary>Where SurveyJS put the Other text, given the effective <c>storeOthersAsComment</c>.</summary>
internal enum OtherStorage
{
    /// <summary>The value keeps "other"; the text is in "{q}-Comment", or the item's 3.x <c>{value, comment}</c> object.</summary>
    Comment,

    /// <summary><c>storeOthersAsComment: false</c>: the typed text replaces "other" in the value.</summary>
    Value,
}
