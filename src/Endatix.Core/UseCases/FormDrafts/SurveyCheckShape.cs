using System.Text.Json;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.FormDrafts;

internal sealed class SurveyCheckShape
{
    private const string CODE = "definition/invalid-shape";
    private readonly Dictionary<JsonElement, string> _replacements = [];
    internal List<FormCheckFinding> Findings { get; } = [];

    internal JsonElement Inspect(JsonElement definition)
    {
        if (definition.ValueKind != JsonValueKind.Object)
        {
            Reject(new(definition, new()), "Use a survey object at the root of the document.", "{}");
        }
        else
        {
            InspectContainer(definition, "", new());
        }
        return _replacements.Count == 0 ? definition : Project(definition);
    }

    private JsonElement Project(JsonElement definition)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteChecked(writer, definition);
        }
        return JsonSerializer.Deserialize<JsonElement>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private void InspectContainer(JsonElement container, string address, FindingPath location)
    {
        foreach (var property in container.EnumerateObject())
        {
            if (property.Name is not ("pages" or "elements" or "templateElements" or "calculatedValues"))
            {
                continue;
            }
            var path = SurveyDiff.Child(address, property.Name);
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                Reject(new(property.Value, location with { Property = property.Name }),
                    $"Use an array for '{property.Name}'.", "[]");
                continue;
            }
            InspectMembers(property, path, location);
        }
    }

    private void InspectMembers(JsonProperty property, string path, FindingPath parent)
    {
        foreach (var (node, address) in SurveyArrayIdentity.Members(property.Value, property.Name, path))
        {
            var location = property.Name == "pages" ? new FindingPath(Page: address) : parent with { Element = address };
            if (node.ValueKind == JsonValueKind.Object)
            {
                InspectContainer(node, address, location);
                continue;
            }
            // Keep indices intact for sibling findings. An inert element contributes no keys.
            var replacement = property.Name is "elements" or "templateElements" ? """{"type":"html"}""" : "{}";
            Reject(new(node, location), $"Use an object for this entry in '{property.Name}'.", replacement);
        }
    }

    private void Reject(LocatedSurveyElement entry, string message, string replacement)
    {
        var path = entry.Path;
        var parent = path.Element ?? path.Page ?? "";
        var address = path.Property is null ? parent : SurveyDiff.Child(parent, path.Property);
        Findings.Add(new($"{CODE}:{address}", CODE, "error", true, false, message, path));
        _replacements[entry.Node] = replacement;
    }

    // Only a malformed document takes this path. The returned projection is used solely
    // for checking unaffected siblings; the version, hash and stored document stay original.
    private void WriteChecked(Utf8JsonWriter writer, JsonElement node)
    {
        if (_replacements.TryGetValue(node, out var replacement))
        {
            writer.WriteRawValue(replacement);
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            WriteObject(writer, node);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            WriteArray(writer, node);
        }
        else
        {
            node.WriteTo(writer);
        }
    }

    private void WriteArray(Utf8JsonWriter writer, JsonElement node)
    {
        writer.WriteStartArray();
        foreach (var item in node.EnumerateArray())
        {
            WriteChecked(writer, item);
        }
        writer.WriteEndArray();
    }

    private void WriteObject(Utf8JsonWriter writer, JsonElement node)
    {
        writer.WriteStartObject();
        foreach (var property in node.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            WriteChecked(writer, property.Value);
        }
        writer.WriteEndObject();
    }
}
