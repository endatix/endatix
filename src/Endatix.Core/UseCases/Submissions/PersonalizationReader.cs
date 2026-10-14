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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? MemberId = null)
{
    /// <summary>
    /// Each frozen value's property name and data type, for reporting. Kept off the wire: the API
    /// returns only the typed <see cref="Variables"/>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<PersonalizationValue> Values { get; init; } = [];
}

/// <summary>The property a frozen value came from, as it was when the snapshot was taken.</summary>
public sealed record PersonalizationValue(string VariableName, string Name, string DataType);

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
            Long(root, "memberId"))
        {
            Values = ValuesOf(root),
        };
    }

    // An entry without a variable name, name or data type is skipped rather than failing the read.
    private static List<PersonalizationValue> ValuesOf(JsonElement root)
    {
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<PersonalizationValue> read = [];
        foreach (var entry in values.EnumerateArray())
        {
            if (TryReadValue(entry, out var value))
            {
                read.Add(value);
            }
        }

        return read;
    }

    private static bool TryReadValue(JsonElement entry, out PersonalizationValue value)
    {
        value = default!;
        if (entry.ValueKind != JsonValueKind.Object
            || Text(entry, "variableName") is not { Length: > 0 } variableName
            || Text(entry, "name") is not { } name
            || Text(entry, "dataType") is not { Length: > 0 } dataType)
        {
            return false;
        }

        value = new PersonalizationValue(variableName, name, dataType);
        return true;
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
