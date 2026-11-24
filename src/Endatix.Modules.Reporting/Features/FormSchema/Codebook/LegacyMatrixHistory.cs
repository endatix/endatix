using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal sealed class LegacyMatrixSourceRequiredException(string question) : ReportingSchemaAliasConflictException(
    $"The stored category identities of matrix '{question}' need a matching historical definition. The previous reporting schema was retained.");

internal sealed record CodebookSource(string? Json, LegacyMatrixHistory? History = null);

// The legacy writer published a label and ordinal; only its source establishes the typed code behind them.
internal sealed record LegacyMatrixWitness(JsonElement Published, JsonElement Typed)
{
    internal bool IsPrefixOf(LegacyMatrixWitness whole) =>
        IsPrefix(Published, whole.Published) && IsPrefix(Typed, whole.Typed);

    private static bool IsPrefix(JsonElement part, JsonElement whole) =>
        part.GetArrayLength() <= whole.GetArrayLength() &&
        part.EnumerateArray().Zip(whole.EnumerateArray()).All(pair => JsonElement.DeepEquals(pair.First, pair.Second));
}

/// <summary>Only historical source definitions, never the incoming definition as a guess at lost codes.</summary>
internal sealed class LegacyMatrixHistory(MergedFormSchema storedMap, IReadOnlyList<string> definitions)
{
    internal JsonElement Recover(string name, JsonElement stored)
    {
        var matrices = MatrixVersions(name);
        var values = stored.EnumerateArray().Select(c => c.GetProperty(SurveyJsPropertyNames.Value).GetString()!).ToList();
        if (MapAgrees(name, values))
        {
            var witnesses = matrices.Select(FormSchemaCodebookBuilder.ReplayLegacyMatrix).ToList();
            var whole = witnesses.Find(w => JsonElement.DeepEquals(w.Published, stored));
            if (whole is not null && witnesses.TrueForAll(w => w.IsPrefixOf(whole)))
            {
                return whole.Typed;
            }
        }

        return RecoverMergedStrings(name, stored, matrices);
    }

    // A readable pre-introduction version published no matrix ids. Once the matrix exists, a missing
    // version or a reused question name breaks continuity; a later matching book cannot close that gap.
    private List<JsonElement> MatrixVersions(string name) =>
        [.. definitions.Select(d => FormSchemaCodebookBuilder.HistoricalRadio(d, name))
            .SkipWhile(matrix => matrix is null)
            .Select(matrix => matrix ?? throw new LegacyMatrixSourceRequiredException(name))];

    // An unmarked, merged string book may never have existed in any one definition. Prove every id
    // from the original typed definitions in publication order, not from the display order or today's definition.
    private JsonElement RecoverMergedStrings(string name, JsonElement stored, List<JsonElement> matrices)
    {
        if (matrices.Count < 2)
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        var values = stored.EnumerateArray().OrderBy(c => c.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32())
            .Select(c => c.GetProperty(SurveyJsPropertyNames.Value).GetString()!).ToList();
        var replay = matrices.Select(matrix => FormSchemaCodebookBuilder.HistoricalStringMatrix(matrix, name))
            .Aggregate((prior, next) => new PublishedMatrixCategories(name).Merge(prior, next, history: null));
        if (!MapAgrees(name, values) || !JsonElement.DeepEquals(stored, replay.GetProperty(SurveyJsPropertyNames.Columns)))
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        return stored;
    }

    private bool MapAgrees(string name, List<string> values)
    {
        var rows = storedMap.Columns.Where(c => c.Kind == FormSchemaColumnKind.MatrixRow && c.SourceQuestion == name).ToList();
        return values.Distinct(StringComparer.Ordinal).Count() == values.Count && rows.Count > 0 &&
            rows.All(r => r.MatrixColumnChoices is { } codes && codes.SequenceEqual(values, StringComparer.Ordinal));
    }
}
