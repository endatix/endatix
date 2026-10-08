using System.Text.Json;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Typed audience values frozen on a submission. Parsed from <c>PersonalizationSnapshot</c>
/// without knowing the audience module.
/// </summary>
public sealed record PersonalizationRead(
    string Identifier,
    DateTimeOffset CapturedAt,
    JsonElement Variables);

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

            string identifier = root.TryGetProperty("identifier", out JsonElement id)
                ? id.GetString() ?? ""
                : "";
            DateTimeOffset capturedAt = root.TryGetProperty("capturedAt", out JsonElement at)
                ? at.GetDateTimeOffset()
                : default;
            return new PersonalizationRead(identifier, capturedAt, variables.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
