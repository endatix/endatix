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

    /// <summary>
    /// A date-time is a plain text question: <c>datetime-local</c> drops the offset every stored value has. A choice
    /// Property that allows other takes free text through <c>allowCustomChoices</c>, stored as the text itself, as
    /// the Audience stores it; a multiple one needs a tagbox for that.
    /// </summary>
    private static JsonObject Question(Property property) =>
        property.DataType switch
        {
            AudienceDataTypeCodes.Number => Named("text", property, ("inputType", "number")),
            AudienceDataTypeCodes.Boolean => Named("boolean", property),
            AudienceDataTypeCodes.Date => Named("text", property, ("inputType", "date")),
            AudienceDataTypeCodes.SingleChoice => Choices("dropdown", property),
            AudienceDataTypeCodes.MultipleChoice => Choices(property.AllowsOther ? "tagbox" : "checkbox", property),
            _ => Named("text", property),
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
}
