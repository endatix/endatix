using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Tabular;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Builds <see cref="IExportColumnPlan"/> from persisted form schema.
/// </summary>
internal static class ExportColumnPlanBuilder
{
    internal static IExportColumnPlan Build(
        FormSchemaEntity schema,
        string locale = "default",
        ColumnAliasProfile aliasProfile = ColumnAliasProfile.Native)
    {
        var flatteningMap = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        var codebookColumns = ReadCodebookColumns(schema.Codebook);
        var aliasTransformer = ResolveAliasTransformer(aliasProfile);

        List<ExportColumnDefinition> columns = [];
        List<ExportColumnAliasInput> aliasInputs = [];

        foreach (string systemKey in SubmissionExportRow.SystemColumns.OrderedKeys)
        {
            aliasInputs.Add(new ExportColumnAliasInput(systemKey, null, null, null, "System"));
            columns.Add(new ExportColumnDefinition(
                CanonicalKey: systemKey,
                ExportKey: systemKey,
                Source: ExportColumnSource.System,
                HeaderLabel: systemKey));
        }

        foreach (var column in flatteningMap.Columns)
        {
            var headerLabel = ResolveHeaderLabel(codebookColumns, column.Key, locale);
            aliasInputs.Add(new ExportColumnAliasInput(
                column.Key,
                column.SourceQuestion,
                column.ChoiceValue,
                column.MatrixRowValue,
                column.Kind.ToString()));

            columns.Add(new ExportColumnDefinition(
                CanonicalKey: column.Key,
                ExportKey: column.Key,
                Source: ExportColumnSource.DataJson,
                HeaderLabel: headerLabel,
                DataType: column.DataType));
        }

        var exportKeys = aliasTransformer.BuildExportKeys(aliasInputs);
        var aliasedColumns = columns
            .Select(column => column with
            {
                ExportKey = exportKeys.TryGetValue(column.CanonicalKey, out string? alias)
                    ? alias
                    : column.CanonicalKey,
            })
            .ToList();

        return new ExportColumnPlan(aliasedColumns);
    }

    private static IColumnAliasTransformer ResolveAliasTransformer(ColumnAliasProfile aliasProfile) =>
        aliasProfile switch
        {
            ColumnAliasProfile.Crunch => CrunchColumnAliasTransformer._instance,
            _ => NativeColumnAliasTransformer._instance,
        };

    private static Dictionary<string, JsonElement> ReadCodebookColumns(string codebookJson)
    {
        Dictionary<string, JsonElement> columns = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(codebookJson))
        {
            return columns;
        }

        using var document = JsonDocument.Parse(codebookJson);
        if (!document.RootElement.TryGetProperty("columns", out var columnsElement) ||
            columnsElement.ValueKind != JsonValueKind.Object)
        {
            return columns;
        }

        foreach (var property in columnsElement.EnumerateObject())
        {
            columns[property.Name] = property.Value.Clone();
        }

        return columns;
    }

    private static string? ResolveHeaderLabel(
        IReadOnlyDictionary<string, JsonElement> codebookColumns,
        string canonicalKey,
        string locale)
    {
        if (!codebookColumns.TryGetValue(canonicalKey, out var columnMetadata))
        {
            return null;
        }

        var title = ReadLocalizedString(columnMetadata, "title", locale);
        var choiceLabel = ReadLocalizedString(columnMetadata, "choiceLabel", locale);
        var rowLabel = ReadLocalizedString(columnMetadata, "rowLabel", locale);
        var columnLabel = ReadLocalizedString(columnMetadata, "columnLabel", locale);

        if (!string.IsNullOrWhiteSpace(choiceLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? choiceLabel : $"{title} ({choiceLabel})";
        }

        if (!string.IsNullOrWhiteSpace(rowLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? rowLabel : $"{title} — {rowLabel}";
        }

        if (!string.IsNullOrWhiteSpace(columnLabel))
        {
            return string.IsNullOrWhiteSpace(title) ? columnLabel : $"{title} — {columnLabel}";
        }

        return title;
    }

    private static string? ReadLocalizedString(JsonElement element, string propertyName, string locale)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (property.TryGetProperty(locale, out var localized) && localized.ValueKind == JsonValueKind.String)
        {
            return localized.GetString();
        }

        if (property.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String)
        {
            return defaultValue.GetString();
        }

        return null;
    }
}
