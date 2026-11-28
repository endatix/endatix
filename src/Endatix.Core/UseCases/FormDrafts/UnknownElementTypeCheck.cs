using System.Collections.Frozen;
using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

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
        var candidates = LocatedSurveyElement.In(input.Definition)
            .Where(element => !_builtIns.Contains(Text(element.Node, "type") ?? "")).ToArray();
        if (candidates.Length == 0)
        {
            return [];
        }
        var definitions = await questions.ListAsync(new ComponentDefinitions(input.TenantId), ct);
        var names = definitions.Select(ComponentName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.Where(e => !names.Contains(Text(e.Node, "type") ?? "")).Select(e => new FormCheckFinding(
            $"element/unknown-type:{e.Path.Element}/type", "element/unknown-type", "error", true, false,
            $"Unknown element type '{Text(e.Node, "type")}'.", e.Path with { Property = "type" })).ToArray();
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

    private sealed class ComponentDefinitions : Specification<CustomQuestion, string>
    {
        internal ComponentDefinitions(long tenantId) => Query.Where(q => q.TenantId == tenantId).Select(q => q.JsonData);
    }
}
