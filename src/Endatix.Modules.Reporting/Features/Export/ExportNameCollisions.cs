using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>What an export name stands for: the question it comes from, and whether the export generated it.</summary>
internal enum ExportNameKind
{
    Ordinary,
    Companion,
    GroupParent,
    Subvariable,
}

internal sealed record ExportNameMeaning(string Question, ExportNameKind Kind)
{
    public bool IsGenerated => Kind is not ExportNameKind.Ordinary;

    public override string ToString() => Kind switch
    {
        ExportNameKind.Companion => $"the Other text of question '{Question}'",
        ExportNameKind.GroupParent => $"the variable that groups question '{Question}'",
        ExportNameKind.Subvariable => $"a column of question '{Question}'",
        _ => $"question '{Question}'",
    };

    // One question can own several names of different kinds (a tagbox's text column and its group); a companion never
    // shares its name with anything.
    public bool Contests(ExportNameMeaning other) =>
        this != other && (Question != other.Question || Kind is ExportNameKind.Companion || other.Kind is ExportNameKind.Companion);
}

/// <summary>One export name that two meanings would share under one naming profile and key separator.</summary>
internal sealed record ExportNameCollision(
    string Name,
    ExportNameMeaning First,
    ExportNameMeaning Second,
    ColumnAliasProfile Profile,
    string Separator)
{
    /// <summary>The question to rename: an ordinary question when one is involved, since generated names follow it.</summary>
    public string Rename => (First.IsGenerated && !Second.IsGenerated ? Second : First).Question;

    public ValidationError Error() => new(
        $"Export name '{Name}' ({Profile.ToString().ToLowerInvariant()} names, separator '{Separator}') would mean both {First} and {Second}. " +
        $"Rename question '{Rename}', then export again.")
    {
        Identifier = "exportNames",
    };
}

/// <summary>
/// The names a Crunch-shaped export generates (panel Other companions, group parents and their subvariables) are
/// fixed by the question they come from, so an export where another question lands on one is refused instead of
/// renamed. Pure: it reads the schema and settings and exports nothing, so a dry run can call it.
/// </summary>
internal static class ExportNameCollisions
{
    public static IReadOnlyList<ExportNameCollision> Find(
        FormSchemaEntity schema,
        ExportFormatSettings settings,
        IColumnAliasTransformerRegistry? registry = null)
    {
        var map = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        using var book = JsonDocument.Parse(schema.Codebook);
        var catalog = new CrunchQuestions(book.RootElement, settings.SpecialChoiceCodes, map);
        var columns = map.Columns.GroupBy(column => column.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        // Shoji keeps group parents under their native alias in every profile.
        var parents = ShojiCodebookGenerator.GroupParentAliases(map, catalog.Entries, settings.KeySeparator)
            .Select(parent => (parent.Alias, Meaning: new ExportNameMeaning(Question(parent.Column), ExportNameKind.GroupParent)))
            .ToList();
        ColumnAliasProfile[] profiles = settings.AliasProfile == ColumnAliasProfile.Native
            ? [ColumnAliasProfile.Native] : [ColumnAliasProfile.Native, settings.AliasProfile];
        return [.. profiles.SelectMany(profile => InProfile(profile))];

        IEnumerable<ExportNameCollision> InProfile(ColumnAliasProfile profile)
        {
            var plan = ExportColumnPlanBuilder.Build(schema, new ExportColumnPlanRequest(
                settings.Locale, profile, KeySeparator: settings.KeySeparator, AliasRegistry: registry, ReadWithShojiCodebook: true));
            var keys = plan.Columns.ToDictionary(column => column.CanonicalKey, column => column.ExportKey, StringComparer.Ordinal);
            var names = plan.Columns.Select(column => (Alias: column.ExportKey, Meaning: Meaning(column.CanonicalKey)))
                .Concat(parents)
                .Concat(catalog.OtherTextKeys.Keys.Select(parent => (
                    Alias: PanelExportColumns.CompanionName(keys[parent], settings.KeySeparator),
                    Meaning: new ExportNameMeaning(Question(columns[parent]), ExportNameKind.Companion))));
            return Contested(names, profile, settings.KeySeparator);
        }

        ExportNameMeaning Meaning(string canonicalKey) =>
            columns.TryGetValue(canonicalKey, out var column)
                ? new(Question(column), column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.MatrixRow or FormSchemaColumnKind.RankingChoice
                    ? ExportNameKind.Subvariable : ExportNameKind.Ordinary)
                : new(canonicalKey, ExportNameKind.Ordinary);
    }

    private static string Question(FormSchemaColumn column) => column.SourceQuestion ?? column.Key;

    private static IEnumerable<ExportNameCollision> Contested(
        IEnumerable<(string Alias, ExportNameMeaning Meaning)> names,
        ColumnAliasProfile profile,
        string separator)
    {
        foreach (var group in names.GroupBy(name => name.Alias, StringComparer.Ordinal))
        {
            var meanings = group.Select(name => name.Meaning).Distinct().ToList();
            var contested = meanings.SelectMany(first => meanings.Where(first.Contests).Select(second => (first, second))).FirstOrDefault();
            if (contested.first is not null)
            {
                yield return new(group.Key, contested.first, contested.second, profile, separator);
            }
        }
    }
}
