using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

internal static partial class FormDefinitionFlattener
{
    private sealed record FlattenContext(
        List<FormSchemaColumn> Columns,
        HashSet<string> SeenKeys,
        SchemaCompilationLimits Limits,
        ChoiceSources Sources)
    {
        public void Add(IEnumerable<FormSchemaColumn> additions)
        {
            foreach (var column in additions)
            {
                AddColumn(Columns, SeenKeys, Limits, column);
            }
        }

        public void AddPanelAnswer(JsonElement template, FormSchemaColumn answer)
        {
            AddColumn(Columns, SeenKeys, Limits, answer);
            Add(ChoiceCommentColumns.For(template, answer.Key, answer.LoopPath)
                .Select(comment => comment with { PanelName = answer.PanelName, PanelIndex = answer.PanelIndex }));
        }
    }
}
