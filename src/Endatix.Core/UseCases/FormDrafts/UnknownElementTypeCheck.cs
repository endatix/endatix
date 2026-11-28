using System.Collections.Frozen;
using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.FormDrafts;

internal sealed class UnknownElementTypeCheck(IRepository<CustomQuestion> questions)
{
    // Creatable survey-core question and panel classes, plus the Hub's registered classes.
    private static readonly FrozenSet<string> _builtIns = new[]
    {
        "panel", "page", "expression", "matrixdropdownbase", "matrixdropdown", "matrixdynamic", "dropdown",
        "matrix", "textbase", "text", "multipletext", "flowpanel", "nonvalue", "empty", "checkbox", "tagbox",
        "ranking", "comment", "file", "html", "radiogroup", "rating", "slider", "boolean", "imagepicker",
        "imagemap", "image", "signaturepad", "paneldynamic", "buttongroup", "richtexteditor", "audiorecorder",
        "dragcategorize"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<FormCheckFinding>> RunAsync(FormCheckInput input, CancellationToken ct)
    {
        var candidates = Walk(input.Definition, "", null)
            .Where(element => !_builtIns.Contains(element.Type)).ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }
        var definitions = await questions.ListAsync(new ComponentDefinitions(input.TenantId), ct);
        var names = definitions.Select(ComponentName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.Where(e => !names.Contains(e.Type)).Select(e => new FormCheckFinding(
            $"element/unknown-type:{e.Path}/type", "element/unknown-type", "error", true, false,
            $"Unknown element type '{e.Type}'.", new(e.Page, e.Path, Property: "type"))).ToArray();
    }

    private static IEnumerable<Element> Walk(JsonElement container, string path, string? page)
    {
        foreach (var property in new[] { "pages", "elements", "templateElements" })
        {
            if (container.ValueKind != JsonValueKind.Object || !container.TryGetProperty(property, out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var item in Members(array, property, new Location(path, page)))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<Element> Members(JsonElement array, string property, Location location)
    {
        var parent = SurveyDiff.Child(location.Path, property);
        var selectors = SurveyArrayIdentity.Index(array, property)?.Keys.ToArray();
        var index = 0;
        foreach (var node in array.EnumerateArray())
        {
            var address = selectors is null ? SurveyDiff.IndexPath(parent, index) : SurveyDiff.Child(parent, selectors[index]);
            index++;
            var currentPage = property == "pages" ? address : location.Page;
            if (property != "pages")
            {
                yield return new Element(Text(node, "type") ?? "", address, currentPage);
            }
            foreach (var child in Walk(node, address, currentPage))
            {
                yield return child;
            }
        }
    }

    private static string? ComponentName(string json)
    {
        using var document = JsonDocument.Parse(json);
        var definition = document.RootElement;
        return string.IsNullOrWhiteSpace(Text(definition, "title")) ? null : Text(definition, "name")?.Trim();
    }

    private static string? Text(JsonElement node, string property) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record Element(string Type, string Path, string? Page);
    private readonly record struct Location(string Path, string? Page);

    private sealed class ComponentDefinitions : Specification<CustomQuestion, string>
    {
        internal ComponentDefinitions(long tenantId) => Query.Where(q => q.TenantId == tenantId).Select(q => q.JsonData);
    }
}
