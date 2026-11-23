using System.Text.Json.Nodes;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>Applies a format's codes to a detached export codebook, using the merged ids across revisions.</summary>
internal sealed class ChoiceCodes(SpecialChoiceCodes? settings)
{
    private readonly Dictionary<string, int> _codes = new Dictionary<string, int?>
    {
        ["none"] = settings?.None,
        ["refused"] = settings?.Refused,
        ["dontknow"] = settings?.DontKnow,
    }.Where(pair => pair.Value.HasValue).ToDictionary(pair => pair.Key, pair => pair.Value!.Value);

    public ValidationError? SettingsError() =>
        _codes.ContainsValue(-1) || _codes.Values.Distinct().Count() != _codes.Count
            ? Error("Special choice codes must be distinct and cannot use the No Data id (-1).")
            : null;

    public bool Configures(ChoiceKind kind) => kind.JsonName() is { } name && _codes.ContainsKey(name);

    public Result<string> Apply(string json)
    {
        if (_codes.Count == 0)
        {
            return json;
        }

        if (SettingsError() is { } error)
        {
            return Result<string>.Invalid(error);
        }

        return Overlay(JsonNode.Parse(json)!);
    }

    private Result<string> Overlay(JsonNode book)
    {
        var catalogs = book["choiceCatalogs"] as JsonObject;
        var questions = book["questions"] as JsonObject;
        if (catalogs is null || HasLegacySelect(questions, catalogs))
        {
            return Result<string>.Invalid(Error("Rebuild the form schema with compile-schema before exporting with special choice codes."));
        }

        return ApplyCatalogs(questions, catalogs).Map(_ => book.ToJsonString());
    }

    private static bool HasLegacySelect(JsonObject? questions, JsonObject catalogs) =>
        questions?.Any(pair => SurveyJsElementType.TryResolve((string?)pair.Value?["surveyJsType"])?.Category == SurveyJsElementCategory.BaseSelect &&
                               catalogs[pair.Key]?["multiple"] is null) == true;

    private Result ApplyCatalogs(JsonObject? questions, JsonObject catalogs)
    {
        foreach (var (name, catalog) in catalogs)
        {
            if (catalog?["multiple"] is null || catalog["choices"] is not JsonArray choices)
            {
                continue;
            }

            var result = ApplyChoices(name, choices, questions?[name]?["choices"] as JsonArray);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private Result ApplyChoices(string question, JsonArray choices, JsonArray? current)
    {
        var entries = choices.OfType<JsonObject>().ToArray();
        var replacements = entries.Where(IsConfigured).ToList();
        if (replacements.Count == 0)
        {
            return Result.Success();
        }
        var error = Collision(question, replacements, entries);
        if (error is not null)
        {
            return Result.Invalid(error);
        }
        foreach (var entry in replacements)
        {
            entry["id"] = _codes[(string)entry["kind"]!];
        }
        AlignCurrentIds(current, entries);
        return Result.Success();
    }

    private bool IsConfigured(JsonObject entry) => entry["kind"] is { } kind && _codes.ContainsKey(kind.GetValue<string>());

    private ValidationError? Collision(string question, List<JsonObject> replacements, JsonObject[] entries)
    {
        foreach (var entry in replacements)
        {
            var code = _codes[(string)entry["kind"]!];
            if (entries.FirstOrDefault(other => other != entry && (int)other["id"]! == code) is { } conflict)
            {
                return Error($"Code {code} for '{entry["value"]}' equals the id of '{conflict["value"]}' in question '{question}'.");
            }
        }
        return null;
    }

    private static void AlignCurrentIds(JsonArray? current, JsonObject[] entries)
    {
        var ids = entries.DistinctBy(entry => (string)entry["value"]!)
            .ToDictionary(entry => (string)entry["value"]!, entry => (int)entry["id"]!);
        foreach (var entry in current?.OfType<JsonObject>() ?? [])
        {
            if (ids.TryGetValue((string)entry["value"]!, out var id))
            {
                entry["id"] = id;
            }
        }
    }

    private static ValidationError Error(string message) => new(message) { Identifier = "specialChoiceCodes" };
}
