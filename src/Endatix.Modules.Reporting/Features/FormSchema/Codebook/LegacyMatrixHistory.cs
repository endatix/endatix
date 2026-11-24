using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal sealed class LegacyMatrixSourceRequiredException(string question) : ReportingSchemaAliasConflictException(
    $"The stored category identities of matrix '{question}' need a matching historical definition. The previous reporting schema was retained.");

internal sealed record CodebookSource(string? Json, LegacyMatrixHistory? History = null);

/// <summary>Only historical source definitions, never the incoming definition as a guess at lost codes.</summary>
internal sealed class LegacyMatrixHistory(MergedFormSchema storedMap, IReadOnlyList<string> definitions)
{
    internal JsonElement Recover(string name, JsonElement stored)
    {
        var values = stored.EnumerateArray().Select(c => c.GetProperty(SurveyJsPropertyNames.Value).GetString()!).ToList();
        if (!MapAgrees(name, values))
        {
            return RecoverMergedStrings(name, stored);
        }

        JsonElement? recovered = null;
        foreach (var definition in definitions)
        {
            if (FormSchemaCodebookBuilder.ReplayLegacyMatrix(definition, name, stored) is { } candidate)
            {
                recovered = Agree(name, recovered, candidate);
            }
        }

        return recovered ?? RecoverMergedStrings(name, stored);
    }

    // An unmarked, merged string book may never have existed in any one definition. Prove every id
    // from the original typed definitions in publication order, not from the display order or today's definition.
    private JsonElement RecoverMergedStrings(string name, JsonElement stored)
    {
        if (definitions.Count < 2)
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        var values = stored.EnumerateArray().OrderBy(c => c.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32())
            .Select(c => c.GetProperty(SurveyJsPropertyNames.Value).GetString()!).ToList();
        var replay = definitions.Select(d => FormSchemaCodebookBuilder.HistoricalStringMatrix(d, name))
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

    private static JsonElement Agree(string name, JsonElement? previous, JsonElement candidate) =>
        previous is { } prior && !JsonElement.DeepEquals(prior, candidate) ? throw new LegacyMatrixSourceRequiredException(name) : candidate;
}
