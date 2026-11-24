using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal sealed record ShojiPanelAliases(PanelExportColumns Native, Dictionary<string, string>? WireAliases, string Separator)
{
    public static ShojiPanelAliases? Of(FormSchemaEntity schema, ExportFormatSettings settings, IColumnAliasTransformerRegistry? registry)
    {
        if (settings.SpecialChoiceCodes is null)
        {
            return null;
        }
        using var doc = JsonDocument.Parse(schema.Codebook);
        var catalog = new CrunchQuestions(doc.RootElement, settings.SpecialChoiceCodes, FormSchemaFlatteningMap.FromJson(schema.FlatteningMap));
        if (catalog.OtherTextKeys.Count == 0)
        {
            return null;
        }
        var separator = settings.KeySeparator == ExportFormatSettings.DefaultKeySeparator
            ? ExportFormatSettings.InterimCrunchKeySeparator : settings.KeySeparator;
        var native = Columns(ColumnAliasProfile.Native);
        return new(native, settings.AliasProfile == ColumnAliasProfile.Native ? null : Columns(settings.AliasProfile).AliasesFrom(native), separator);

        PanelExportColumns Columns(ColumnAliasProfile profile) => new(ExportColumnPlanBuilder.Build(schema,
            new ExportColumnPlanRequest(settings.Locale, profile, KeySeparator: separator, AliasRegistry: registry, ReadWithShojiCodebook: true)), catalog, separator);
    }

    // Metadata keys and folder/order references remain paired. Crunch reads CSV columns by the variable's alias.
    public string Apply(string shoji)
    {
        if (WireAliases is null)
        {
            return shoji;
        }
        var root = JsonNode.Parse(shoji)!;
        foreach (var variable in root["body"]!["table"]!["metadata"]!.AsObject().Select(kv => kv.Value!))
        {
            ApplyVariable(variable);
        }
        return root.ToJsonString();
    }

    private void ApplyVariable(JsonNode variable)
    {
        if (variable["subvariables"] is not JsonArray children)
        {
            ApplyAlias(variable);
            return;
        }
        foreach (var child in children)
        {
            ApplyAlias(child!);
        }
    }

    private void ApplyAlias(JsonNode variable)
    {
        if ((string?)variable["alias"] is { } name && WireAliases!.TryGetValue(name, out var alias))
        {
            variable["alias"] = alias;
        }
    }
}
