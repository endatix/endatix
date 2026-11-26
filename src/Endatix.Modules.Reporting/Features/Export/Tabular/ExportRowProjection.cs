using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

internal sealed class ExportRowProjection
{
    private readonly Dictionary<(string Key, string Value), int> _cells = [];
    private readonly Func<FlattenedExportRow, string>? _crunch;
    private readonly Dictionary<string, SubmittedQuestions?> _definitions = new(StringComparer.Ordinal);
    private readonly CrunchQuestions? _catalog;

    public bool NeedsSubmittedChoices => _catalog?.OtherTextKeys.Count > 0;

    public ExportRowProjection(FormSchemaEntity schema, SpecialChoiceCodes? settings, bool crunch)
    {
        var map = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        if (crunch)
        {
            using var book = JsonDocument.Parse(schema.Codebook);
            _catalog = new CrunchQuestions(book.RootElement, settings, map);
            _crunch = row => new CrunchTabularValueProjector(_catalog, Submitted(row)).Project(row, map);
        }

        if (settings is not null)
        {
            IndexCells(map, schema.Codebook, new ChoiceCodes(settings));
        }
    }

    private void IndexCells(MergedFormSchema map, string codebook, ChoiceCodes codes)
    {
        var catalogs = ChoiceCatalogs.Read(codebook);
        foreach (var column in map.Columns.Where(NeedsSpecialCellCode))
        {
            var choices = catalogs.For(column.SourceQuestion ?? column.Key).Choices;
            foreach (var choice in choices.Where(choice => codes.Configures(choice.Kind)))
            {
                _cells[(column.Key, choice.Value)] = choice.Id;
            }
        }
    }

    private bool NeedsSpecialCellCode(FormSchemaColumn column) =>
        column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.LoopSource ||
        (_crunch is null && column.Kind is FormSchemaColumnKind.PanelDynamicIndex or FormSchemaColumnKind.NestedLoop);

    private PanelOtherAnswers? Submitted(FlattenedExportRow row)
    {
        if (!NeedsSubmittedChoices || row.SubmittedDefinition is not { } definition || row.SourceAnswersJson is not { } answers)
        {
            return null;
        }
        if (!_definitions.TryGetValue(definition, out var submitted))
        {
            submitted = CopyDefinition.QuestionsIn(definition);
            _definitions.Add(definition, submitted);
        }
        return new(submitted, JsonSerializer.Deserialize<JsonElement>(answers));
    }

    public ExportColumnPlan WithCompanions(ExportColumnPlan plan, Func<ExportColumnPlan> unscoped, string separator)
    {
        if (!NeedsSubmittedChoices)
        {
            return plan;
        }
        return new PanelExportColumns(unscoped(), _catalog!, separator).WithCompanions(plan);
    }

    public string Project(FlattenedExportRow source)
    {
        var json = _crunch?.Invoke(source) ?? source.DataJson;
        if (_cells.Count == 0)
        {
            return json;
        }

        var row = JsonNode.Parse(json)!.AsObject();
        foreach (var (key, value) in row.ToArray())
        {
            if (value is JsonValue cell && cell.TryGetValue<string>(out var answer) && _cells.TryGetValue((key, answer), out var code))
            {
                row[key] = code;
            }
        }

        return row.ToJsonString();
    }
}
