using System.Text.Json;
using System.Text.Json.Serialization;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Typed audience values frozen on a submission. Parsed from <c>PersonalizationSnapshot</c>
/// without knowing the audience module.
/// </summary>
public sealed record PersonalizationRead(
    string Identifier,
    DateTimeOffset CapturedAt,
    JsonElement Variables,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MemberId = null);

public static class PersonalizationReader
{
    public static PersonalizationRead? Read(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("variables", out JsonElement variables)
                || variables.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return FromRoot(root, variables.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PersonalizationRead FromRoot(JsonElement root, JsonElement variables) =>
        new(
            Text(root, "identifier") ?? "",
            root.TryGetProperty("capturedAt", out JsonElement at) ? at.GetDateTimeOffset() : default,
            variables,
            Text(root, "source"),
            Long(root, "memberId"));

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;

    private static long? Long(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long number)
            ? number
            : null;
}
