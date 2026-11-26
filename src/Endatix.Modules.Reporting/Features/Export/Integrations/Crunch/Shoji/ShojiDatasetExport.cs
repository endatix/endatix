using Endatix.Core.Abstractions.Repositories;
using Endatix.Modules.Reporting.Contracts.Export;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal sealed class ShojiDatasetExport(IFormsRepository forms, IColumnAliasTransformerRegistry aliases)
{
    public async Task<string> GenerateAsync(FormSchemaEntity schema, ExportFormatSettings settings, CancellationToken ct)
    {
        var locale = ReportingExportSchemaHelper.ResolveLocaleOrDefault(settings.Locale);
        var form = await forms.GetByIdAsync(schema.FormId, ct);
        var title = Title(form?.Name, locale);
        var description = string.IsNullOrWhiteSpace(form?.Description)
            ? $"Exported shoji codebook for formID {schema.FormId}" : form.Description.Trim();
        var columns = ShojiPanelAliases.Of(schema, settings, aliases);
        var dataset = new ShojiCodebookGenerator.Dataset(columns?.Separator ?? settings.KeySeparator, locale,
            title, description, settings.SpecialChoiceCodes, columns);
        var shoji = ShojiCodebookGenerator.ForDataset(schema.FlatteningMap, schema.Codebook, dataset);
        return columns?.Apply(shoji) ?? shoji;
    }

    /// <summary>The names this codebook would give two meanings, under the separator it is written with.</summary>
    public IReadOnlyList<ExportNameCollision> Collisions(FormSchemaEntity schema, ExportFormatSettings settings) =>
        ExportNameCollisions.Find(new(
            schema, settings with { KeySeparator = ShojiPanelAliases.SeparatorFor(schema, settings) }, ExportNameFile.ShojiCodebook, aliases));

    private static string? Title(string? name, string locale)
    {
        var title = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        return ReportingExportSchemaHelper.IsDefaultLocale(locale)
            ? title : ReportingExportSchemaHelper.FormatTitleWithLocale(title ?? "Form export", locale);
    }
}
