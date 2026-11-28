using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

internal static partial class FormDefinitionFlattener
{
    internal static ColumnCheck Check(JsonElement definition, SchemaCompilationLimits limits)
    {
        var check = new ColumnCheck();
        try
        {
            var elements = CollectElements(definition, limits);
            check.QuestionCount = CountQuestions(elements, definition);
            EmitColumns(definition, elements, new(check, limits, ChoiceSourcesOf(elements)));
        }
        catch (SchemaCompilationLimitExceededException limit)
        {
            // A bounded expansion can end early. Preserve everything observed before it.
            check.ExpansionLimit = limit;
        }
        return check;
    }

    internal abstract class ColumnOutput
    {
        private readonly HashSet<string> _ordinaryKeys = new(StringComparer.Ordinal);

        public void Add(FormSchemaColumn column)
        {
            // Choice-comment keys are candidates; the compiler assigns free aliases afterwards.
            if (column.Kind != FormSchemaColumnKind.ChoiceComment && !_ordinaryKeys.Add(column.Key))
            {
                Duplicate(column.Key);
            }
            Accept(column);
        }

        protected abstract void Duplicate(string key);
        protected abstract void Accept(FormSchemaColumn column);
    }

    private sealed class CompiledColumns(int maxColumns) : ColumnOutput
    {
        public List<FormSchemaColumn> Columns { get; } = [];

        protected override void Duplicate(string key) => throw new SchemaCompilationLimitExceededException(
            SchemaCompilationLimitKind.DuplicateColumnKey, 1, $"Duplicate form schema column key '{key}'.", context: key);

        protected override void Accept(FormSchemaColumn column)
        {
            if (Columns.Count >= maxColumns)
            {
                ThrowLimitExceeded(SchemaCompilationLimitKind.MaxColumns, maxColumns, Columns.Count + 1);
            }
            Columns.Add(column);
        }
    }

    internal sealed class ColumnCheck : ColumnOutput
    {
        public HashSet<string> DuplicateKeys { get; } = new(StringComparer.Ordinal);
        public int QuestionCount { get; internal set; }
        public int ColumnCount { get; private set; }
        public SchemaCompilationLimitExceededException? ExpansionLimit { get; internal set; }

        protected override void Duplicate(string key) => DuplicateKeys.Add(key);
        protected override void Accept(FormSchemaColumn column) => ColumnCount++;
    }
}
