using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

internal static partial class FormDefinitionFlattener
{
    internal static ColumnCheck Check(JsonElement definition, SchemaCompilationLimits limits)
    {
        var check = new ColumnCheck(limits.MaxColumns);
        limits = limits with { OnIncomplete = check.RecordIncomplete };
        var elements = CollectElements(definition, limits);
        check.QuestionCount = CountQuestions(elements, definition);
        EmitColumns(definition, elements, new(check, limits, ChoiceSourcesOf(elements)));
        return check;
    }

    internal abstract class ColumnOutput
    {
        private readonly HashSet<string> _ordinaryKeys = new(StringComparer.Ordinal);

        public void Add(FormSchemaColumn column)
        {
            CheckCapacity(column);
            // Choice-comment keys are candidates; the compiler assigns free aliases afterwards.
            if (column.Kind != FormSchemaColumnKind.ChoiceComment && !_ordinaryKeys.Add(column.Key))
            {
                Duplicate(column.Key);
            }
            Accept(column);
        }

        protected abstract void Duplicate(string key);
        protected abstract void Accept(FormSchemaColumn column);
        protected virtual void CheckCapacity(FormSchemaColumn column) { }
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

    internal sealed class ColumnCheck(int maxColumns) : ColumnOutput
    {
        private int _expandedColumns;
        public HashSet<string> DuplicateKeys { get; } = new(StringComparer.Ordinal);
        public int QuestionCount { get; internal set; }
        public int ColumnCount { get; private set; }
        public Dictionary<JsonElement, SchemaCompilationLimitExceededException> Incomplete { get; } = [];
        internal void RecordIncomplete(JsonElement element, SchemaCompilationLimitExceededException limit) =>
            Incomplete.TryAdd(element, limit);

        protected override void Duplicate(string key) => DuplicateKeys.Add(key);
        protected override void Accept(FormSchemaColumn column) => ColumnCount++;

        protected override void CheckCapacity(FormSchemaColumn column)
        {
            // Direct keys cost at most one per input question. Bound generated expansions
            // separately so exhausting the budget cannot hide a later direct sibling.
            if (column.Kind is FormSchemaColumnKind.Simple or FormSchemaColumnKind.Calculated or FormSchemaColumnKind.FileUpload)
            {
                return;
            }
            if (_expandedColumns >= maxColumns)
            {
                ThrowLimitExceeded(SchemaCompilationLimitKind.MaxColumns, maxColumns, _expandedColumns + 1);
            }
            _expandedColumns++;
        }
    }
}
