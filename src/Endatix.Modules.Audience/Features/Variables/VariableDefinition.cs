using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// A form's Properties as the SurveyJS variable definition: one top-level question per Property, named by its
/// variable name and titled by its name. The only data type → question mapping; the values it checks are typed by
/// <see cref="AudienceValueJson.Typed"/>.
/// </summary>
internal static class VariableDefinition
{
    public static JsonObject Build(IReadOnlyList<Property> properties) =>
        new() { ["elements"] = new JsonArray([.. properties.Select(Question)]) };

    private static JsonObject Question(Property property)
    {
        JsonObject question = QuestionOf(property);
        question["prefillTargets"] = new JsonArray([.. PrefillTargets(property.DataType).Select(target => target.ToJson())]);
        return question;
    }

    /// <summary>
    /// A date-time is a plain text question: <c>datetime-local</c> drops the offset every stored value has. A choice
    /// Property that allows other takes free text through <c>allowCustomChoices</c>, stored as the text itself, as
    /// the Audience stores it; a multiple one needs a tagbox for that.
    /// </summary>
    private static JsonObject QuestionOf(Property property) =>
        property.DataType switch
        {
            AudienceDataTypeCodes.Number => Named("text", property, ("inputType", "number")),
            AudienceDataTypeCodes.Boolean => Named("boolean", property),
            AudienceDataTypeCodes.Date => Named("text", property, ("inputType", "date")),
            AudienceDataTypeCodes.SingleChoice => Choices("dropdown", property),
            AudienceDataTypeCodes.MultipleChoice => Choices(property.AllowsOther ? "tagbox" : "checkbox", property),
            _ => Named("text", property),
        };

    /// <summary>
    /// The form questions whose answer has the shape the Snapshot types this data type as, so a designer can prefill
    /// them from it: a string, a number, a boolean, a date string, one key, or an array of keys. A date-time fills a
    /// plain text question for the same reason its own question is one. Whether a choice Property allows other does
    /// not change its targets.
    /// </summary>
    private static PrefillTarget[] PrefillTargets(string dataType) =>
        dataType switch
        {
            AudienceDataTypeCodes.Number => [new("text", "number"), new("rating"), new("slider")],
            AudienceDataTypeCodes.Boolean => [new("boolean")],
            AudienceDataTypeCodes.Date => [new("text", "date")],
            AudienceDataTypeCodes.DateTime => [new("text")],
            AudienceDataTypeCodes.SingleChoice => [new("dropdown"), new("radiogroup"), new("buttongroup")],
            AudienceDataTypeCodes.MultipleChoice => [new("checkbox"), new("tagbox")],
            AudienceDataTypeCodes.Text => [new("text"), new("comment")],
            _ => [new("text")],
        };

    /// <summary>The keys in the order the author gave them; a choice list has no labels yet.</summary>
    private static JsonObject Choices(string type, Property property)
    {
        JsonObject question = Named(type, property);
        List<string> keys = PropertyValueRules.ParseKeys(property.ChoicesJson) ?? [];
        question["choices"] = new JsonArray([.. keys.Select(key => JsonValue.Create(key))]);
        if (property.AllowsOther)
        {
            question["allowCustomChoices"] = true;
        }

        return question;
    }

    private static JsonObject Named(string type, Property property, params (string Key, string Value)[] settings)
    {
        JsonObject question = new()
        {
            ["type"] = type,
            ["name"] = property.VariableName,
            ["title"] = property.Name,
        };
        foreach ((string key, string value) in settings)
        {
            question[key] = value;
        }

        return question;
    }

    /// <summary>A SurveyJS question type, and its <c>inputType</c> when it needs one, that a Property can prefill.</summary>
    private sealed record PrefillTarget(string Type, string? InputType = null)
    {
        /// <summary><c>inputType</c> is left out when the question takes its default.</summary>
        public JsonObject ToJson()
        {
            JsonObject json = new() { ["type"] = Type };
            if (InputType is not null)
            {
                json["inputType"] = InputType;
            }

            return json;
        }
    }
}
