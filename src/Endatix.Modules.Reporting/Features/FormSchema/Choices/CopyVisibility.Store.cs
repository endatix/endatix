using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

// STORE: how a copy's rule is kept on its choice catalog, read back, and merged across compiled definitions.
internal sealed partial class CopyVisibility
{
    /// <summary>
    /// Writes a copy's catalog fields: <c>copied</c> on any copy, and <c>shown</c> on a checkbox or tagbox whose shown
    /// choices follow from the answers (only those have indicator columns).
    /// </summary>
    public static void WriteFor(Utf8JsonWriter writer, JsonElement question, Func<CopyVisibility?> rule)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward)
        {
            return;
        }

        writer.WriteBoolean(FormSchemaCodebookPropertyNames.Copied, true);
        if (question.IsMultiSelectBaseSelect())
        {
            rule()?.WriteTo(writer);
        }
    }

    /// <summary>Whether a choice catalog is marked as a copy's.</summary>
    public static bool IsCopied(JsonElement catalog) =>
        catalog.ValueKind == JsonValueKind.Object && catalog.GetBooleanProperty(FormSchemaCodebookPropertyNames.Copied);

    /// <summary>The catalog with <c>copied</c> set, for a codebook compiled before the marker existed.</summary>
    public static JsonElement MarkCopied(JsonElement catalog)
    {
        if (IsCopied(catalog) || catalog.ValueKind != JsonValueKind.Object)
        {
            return catalog;
        }

        var marked = System.Text.Json.Nodes.JsonNode.Parse(catalog.GetRawText())!.AsObject();
        marked[FormSchemaCodebookPropertyNames.Copied] = true;
        return JsonSerializer.SerializeToElement(marked);
    }

    /// <summary>Writes <c>"shown": {mode, sources: [{name, values}], whenComplete}</c>.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(FormSchemaCodebookPropertyNames.Shown);
        writer.WriteString(FormSchemaCodebookPropertyNames.Mode, CopyModes.ToJsonName(Mode));
        writer.WriteStartArray(FormSchemaCodebookPropertyNames.Sources);
        foreach (var source in Sources)
        {
            source.WriteTo(writer);
        }

        writer.WriteEndArray();
        if (WhenComplete)
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.WhenComplete, true);
        }

        writer.WriteEndObject();
    }

    /// <summary>The rule on a choice catalog; null when it has none or it is malformed (sources without values too).</summary>
    public static CopyVisibility? Read(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object ||
            catalog.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Shown) is not { ValueKind: JsonValueKind.Object } shown ||
            CopyModes.ParseExact(shown.GetStringProperty(FormSchemaCodebookPropertyNames.Mode)) is not { } mode ||
            !shown.TryGetArrayProperty(FormSchemaCodebookPropertyNames.Sources, out var sourcesArray))
        {
            return null;
        }

        List<SourceChoices> sources = [.. sourcesArray.EnumerateArray().Select(ReadSource).OfType<SourceChoices>()];
        return sources.Count == sourcesArray.GetArrayLength() && sources.Count > 0
            ? new CopyVisibility(mode, sources, shown.GetBooleanProperty(FormSchemaCodebookPropertyNames.WhenComplete))
            : null;
    }

    private static SourceChoices? ReadSource(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object ||
            source.GetNonEmptyStringProperty(FormSchemaCodebookPropertyNames.SourceName) is not { } name ||
            !source.TryGetArrayProperty(FormSchemaCodebookPropertyNames.Values, out var values))
        {
            return null;
        }

        List<string> offered = [.. values.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null).OfType<string>()];
        return offered.Count == values.GetArrayLength() ? new SourceChoices(name, offered) : null;
    }

    /// <summary>
    /// MERGE (a compile over an earlier definition's codebook): the rule is kept only when the stored and the new rule
    /// are the same, each source offers the same values, and the copy offers the same choices and special items. Otherwise it is dropped, and stays dropped
    /// until a replace compile, so a row saved under an earlier definition is never judged by a rule it was not shown
    /// under. The <c>copied</c> marker is kept whenever either catalog has it.
    /// </summary>
    public static void Merge(Utf8JsonWriter writer, JsonElement existingCatalog, JsonElement currentCatalog)
    {
        if (IsCopied(existingCatalog) || IsCopied(currentCatalog))
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.Copied, true);
        }

        if (Read(existingCatalog) is { } stored &&
            Read(currentCatalog) is { } current &&
            stored.SameAs(current) &&
            CatalogValues(existingCatalog).SetEquals(CatalogValues(currentCatalog)))
        {
            current.WriteTo(writer);
        }
    }

    // Every value with its kind: the copied choices and the copy's own special items.
    private static HashSet<(string, ChoiceKind)> CatalogValues(JsonElement catalog) =>
        catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out var entries)
            ? [.. entries.EnumerateArray()
                .Select(CatalogChoice.Read)
                .OfType<CatalogChoice>()
                .Select(choice => (choice.Value, choice.Kind))]
            : [];
}
