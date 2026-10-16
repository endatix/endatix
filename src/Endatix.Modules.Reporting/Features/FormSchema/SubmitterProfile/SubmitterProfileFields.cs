using System.Text.Json;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;

/// <summary>
/// The submitter profile fields prepared submissions carry: <see cref="ReportingOptions.SubmitterProfileFields"/>.
/// The profile snapshot holds personal data, so a field reaches reporting only while it is listed here, even when a
/// schema merged before it was removed still has its column.
/// </summary>
internal sealed class SubmitterProfileFields
{
    public static readonly SubmitterProfileFields None = new([]);

    private readonly HashSet<string> _fields;

    /// <param name="fields">The fields in column order; blank and repeated ones are ignored.</param>
    public SubmitterProfileFields(IEnumerable<string> fields)
    {
        List<string> ordered = fields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _fields = new HashSet<string>(ordered, StringComparer.Ordinal);
        Columns = ordered.Select(SubmitterProfileColumns.Column).ToList();
    }

    /// <summary>One column per field, in the configured order.</summary>
    public IReadOnlyList<FormSchemaColumn> Columns { get; }

    public static SubmitterProfileFields From(ReportingOptions options) => new(options.SubmitterProfileFields);

    /// <summary>
    /// The listed fields a profile snapshot holds; null when it holds none. An unreadable snapshot is no profile,
    /// so a flatten never fails on it.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Read(string? snapshot)
    {
        if (_fields.Count == 0 || string.IsNullOrWhiteSpace(snapshot))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(snapshot);
            return ListedValues(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Dictionary<string, string>? ListedValues(JsonElement profile)
    {
        if (profile.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (var field in profile.EnumerateObject().Where(IsListedText))
        {
            values[field.Name] = field.Value.GetString();
        }

        return values.Count == 0 ? null : values;
    }

    private bool IsListedText(JsonProperty field) =>
        field.Value.ValueKind == JsonValueKind.String && _fields.Contains(field.Name);
}
