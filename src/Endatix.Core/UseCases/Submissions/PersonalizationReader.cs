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

/// <summary>
/// Every submission read maps through this reader, so it never throws: unreadable JSON or a snapshot
/// without <c>variables</c> is no personalization, and a field of an unexpected type (a numeric
/// <c>capturedAt</c>, a numeric <c>identifier</c>) reads as its default.
/// </summary>
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
            using var document = JsonDocument.Parse(snapshot);
            return FromRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PersonalizationRead? FromRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("variables", out var variables)
            || variables.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PersonalizationRead(
            Text(root, "identifier") ?? "",
            CapturedAt(root),
            variables.Clone(),
            Text(root, "source"),
            Long(root, "memberId"));
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset CapturedAt(JsonElement root) =>
        root.TryGetProperty("capturedAt", out var at)
        && at.ValueKind == JsonValueKind.String
        && at.TryGetDateTimeOffset(out var capturedAt)
            ? capturedAt
            : default;

    // TryGetInt64 throws on a non-number, so the kind is checked first ("memberId": "123" or null).
    private static long? Long(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;
}
