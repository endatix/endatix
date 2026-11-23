using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

internal static class ConditionalChoices
{
    public const string PROPERTY_NAME = "conditionalChoices";

    public static bool HasListCondition(JsonElement question) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesVisibleIf) is not null;

    public static IEnumerable<string> ValuesIn(JsonElement question)
    {
        var all = HasListCondition(question);
        return SurveyJsChoiceHelper.EnumerateChoiceElements(question)
            .Where(choice => all || choice.Element.TryGetPropertyValue(SurveyJsPropertyNames.VisibleIf)?.GetNonEmptyStringValue() is not null)
            .Select(choice => choice.Value);
    }

    public static IEnumerable<string>? Read(JsonElement catalog) =>
        catalog.TryGetPropertyValue(PROPERTY_NAME) is { ValueKind: JsonValueKind.Array } values
            ? values.EnumerateArray().Select(value => value.GetString()).OfType<string>()
            : null;

    public static void Merge(Utf8JsonWriter writer, JsonElement existing, JsonElement current) =>
        Write(writer, (Read(existing) ?? []).Concat(Read(current) ?? []));

    public static void Write(Utf8JsonWriter writer, IEnumerable<string> values)
    {
        string[] choices = [.. values.Distinct(StringComparer.Ordinal)];
        if (choices.Length > 0)
        {
            writer.WritePropertyName(PROPERTY_NAME);
            JsonSerializer.Serialize(writer, choices);
        }
    }
}
