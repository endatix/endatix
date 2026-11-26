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
    /// <summary>The guide section on recovery: what a rename clears, and the routes once the clashing version has responses.</summary>
    public const string GuideUrl = "https://docs.endatix.com/docs/developers/api/special-answers-in-exports#refused-export-names";

    /// <summary>The question to rename: an ordinary question when one is involved, since generated names follow it.</summary>
    public string Rename => (First.IsGenerated && !Second.IsGenerated ? Second : First).Question;

    public ValidationError Error() => new(
        $"Export name '{Name}' ({Profile.ToString().ToLowerInvariant()} names, separator '{Separator}') would mean both {First} and {Second}. " +
        $"To fix it, rename question '{Rename}' before that form version has responses; otherwise see {GuideUrl}.")
    {
        Identifier = "exportNames",
    };
}

/// <summary>The file whose names are checked: a tabular export (CSV, JSON, XLSX) or the Shoji codebook.</summary>
internal enum ExportNameFile
{
    Tabular,
    ShojiCodebook,
}

/// <summary>What <see cref="ExportNameCollisions.Find"/> reads: the compiled schema, the format's resolved settings and the file.</summary>
internal sealed record ExportNameRequest(
    FormSchemaEntity Schema,
    ExportFormatSettings Settings,
    ExportNameFile File,
    IColumnAliasTransformerRegistry? Registry = null);

/// <summary>
/// The names a Crunch-shaped export generates (panel Other companions, group parents and their subvariables) are
/// fixed by the question they come from, so an export where another question lands on one is refused instead of
/// renamed. Pure: it reads the schema and settings and exports nothing, so a dry run can call it.
/// </summary>
internal static class ExportNameCollisions
{
    public static IReadOnlyList<ExportNameCollision> Find(ExportNameRequest request)
    {
        var (schema, settings, _, _) = request;
        var map = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        using var book = JsonDocument.Parse(schema.Codebook);
        Names names = new(request, map, new CrunchQuestions(book.RootElement, settings.SpecialChoiceCodes, map));
        ColumnAliasProfile[] profiles = settings.AliasProfile == ColumnAliasProfile.Native
            ? [ColumnAliasProfile.Native] : [ColumnAliasProfile.Native, settings.AliasProfile];
        return [.. profiles.SelectMany(profile => Contested(names.In(profile), profile, settings.KeySeparator))];
    }

    /// <summary>Every name one export file builds, ordinary and generated, with what it stands for.</summary>
    private sealed class Names(ExportNameRequest request, MergedFormSchema map, CrunchQuestions catalog)
    {
        private readonly ExportFormatSettings _settings = request.Settings;

        private readonly Dictionary<string, FormSchemaColumn> _columns = map.Columns
            .GroupBy(column => column.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        // Shoji keeps group parents under their native alias in every profile.
        private readonly List<(string Alias, ExportNameMeaning Meaning)> _parents = [.. ShojiCodebookGenerator
            .GroupParentAliases(map, catalog.Entries, request.Settings.KeySeparator)
            .Select(parent => (parent.Alias, new ExportNameMeaning(Question(parent.Column), ExportNameKind.GroupParent)))];

        public IEnumerable<(string Alias, ExportNameMeaning Meaning)> In(ColumnAliasProfile profile)
        {
            var plan = ExportColumnPlanBuilder.Build(request.Schema, new ExportColumnPlanRequest(
                _settings.Locale, profile, KeySeparator: _settings.KeySeparator, AliasRegistry: request.Registry, ReadWithShojiCodebook: true));
            var keys = plan.Columns.ToDictionary(column => column.CanonicalKey, column => column.ExportKey, StringComparer.Ordinal);
            return plan.Columns.Select(column => (column.ExportKey, Meaning(column.CanonicalKey)))
                .Concat(WritesParents(profile) ? _parents : [])
                .Concat(catalog.OtherTextKeys.Keys.Select(parent => (
                    PanelExportColumns.CompanionName(keys[parent], _settings.KeySeparator),
                    new ExportNameMeaning(Question(_columns[parent]), ExportNameKind.Companion))));
        }

        // Only the Shoji codebook writes group parents. They keep their native alias, so they sit next to a profile's
        // aliases only once codes make the codebook rename its other variables to that profile.
        private bool WritesParents(ColumnAliasProfile profile) =>
            request.File is ExportNameFile.ShojiCodebook &&
            (profile is ColumnAliasProfile.Native || _settings.SpecialChoiceCodes is not null);

        private ExportNameMeaning Meaning(string canonicalKey) =>
            _columns.TryGetValue(canonicalKey, out var column)
                ? new(Question(column), column.Kind is FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.MatrixRow or FormSchemaColumnKind.RankingChoice
                    ? ExportNameKind.Subvariable : ExportNameKind.Ordinary)
                : new(canonicalKey, ExportNameKind.Ordinary);
    }

    /// <summary>The 400 an export returns: one error per contested name, each with both meanings and the question to rename.</summary>
    public static Result<T> Refusal<T>(IReadOnlyList<ExportNameCollision> collisions) =>
        Result<T>.Invalid([.. collisions.Select(collision => collision.Error())]);

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
