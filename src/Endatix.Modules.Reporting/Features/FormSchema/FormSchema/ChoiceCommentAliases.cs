using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>Ordinary columns keep their keys. A published comment also keeps its key across compiles.</summary>
internal sealed class ChoiceCommentAliases
{
    private readonly HashSet<string> _reserved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FormSchemaColumn> _publishedByAlias = new(StringComparer.Ordinal);
    private readonly Dictionary<CommentSource, string> _publishedBySource = [];

    public static IReadOnlyList<FormSchemaColumn> Assign(IReadOnlyList<FormSchemaColumn> columns, MergedFormSchema? published)
    {
        ChoiceCommentAliases names = new();
        foreach (var column in published?.Columns ?? [])
        {
            names.RememberPublished(column);
        }

        foreach (var column in columns.Where(column => column.Kind != FormSchemaColumnKind.ChoiceComment))
        {
            names.ReserveOrdinary(column);
        }

        return columns.Select(column => column.Kind == FormSchemaColumnKind.ChoiceComment ? names.Assign(column) : column).ToList();
    }

    private void RememberPublished(FormSchemaColumn column)
    {
        _reserved.UnionWith(Aliases(column));
        if (column.Kind == FormSchemaColumnKind.ChoiceComment)
        {
            _publishedBySource.Add(CommentSource.Of(column), column.Key);
            _publishedByAlias.Add(Alias(column.Key), column);
        }
    }

    private void ReserveOrdinary(FormSchemaColumn column)
    {
        foreach (var alias in Aliases(column))
        {
            if (_publishedByAlias.TryGetValue(alias, out var previous))
            {
                throw new ReportingSchemaAliasConflictException(alias, previous, column);
            }

            _reserved.Add(alias);
        }
    }

    private FormSchemaColumn Assign(FormSchemaColumn comment)
    {
        if (_publishedBySource.TryGetValue(CommentSource.Of(comment), out var existing))
        {
            return comment with { Key = existing };
        }

        var key = comment.Key;
        var suffix = 2;
        while (!_reserved.Add(Alias(key)))
        {
            key = $"{comment.Key}_{suffix++}";
        }

        return comment with { Key = key };
    }

    private static IEnumerable<string> Aliases(FormSchemaColumn column)
    {
        yield return Alias(column.Key);
        if (column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.MatrixRow)
        {
            yield return Alias(ExportKeyTransformer.RemoveLastSegment(column.Key));
        }
    }

    private static string Alias(string key) => ExportKeyTransformer.Transform(key, ExportFormatSettings.InterimCrunchKeySeparator);

    private sealed record CommentSource(string Question, string Choice, string? Panel, int? Index, string? Path)
    {
        public static CommentSource Of(FormSchemaColumn column) => new(
            column.SourceQuestion!, column.ChoiceValue!, column.PanelName, column.PanelIndex,
            column.LoopPath is null ? null : System.Text.Json.JsonSerializer.Serialize(column.LoopPath));
    }
}
