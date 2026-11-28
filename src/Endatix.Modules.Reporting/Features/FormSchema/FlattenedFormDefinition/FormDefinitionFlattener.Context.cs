using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

internal static partial class FormDefinitionFlattener
{
    private sealed record FlattenContext(
        ColumnOutput Output,
        SchemaCompilationLimits Limits,
        ChoiceSources Sources)
    {
        public void Add(IEnumerable<FormSchemaColumn> additions)
        {
            foreach (var column in additions)
            {
                Output.Add(column);
            }
        }

        public void AddPanelAnswer(JsonElement template, FormSchemaColumn answer)
        {
            Output.Add(answer);
            Add(ChoiceCommentColumns.For(template, answer.Key, answer.LoopPath)
                .Select(comment => comment with { PanelName = answer.PanelName, PanelIndex = answer.PanelIndex }));
        }
    }
}
