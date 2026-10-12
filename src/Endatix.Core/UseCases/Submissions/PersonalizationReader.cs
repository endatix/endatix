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

        return new PersonalizationRead(Identifier(root), CapturedAt(root), variables.Clone());
    }

    private static string Identifier(JsonElement root) =>
        root.TryGetProperty("identifier", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() ?? ""
            : "";

    private static DateTimeOffset CapturedAt(JsonElement root) =>
        root.TryGetProperty("capturedAt", out var at)
        && at.ValueKind == JsonValueKind.String
        && at.TryGetDateTimeOffset(out var capturedAt)
            ? capturedAt
            : default;
}
