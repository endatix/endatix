using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// A form's Audience as the variables the designer reads, from one read: the definition and one preset per participant
/// read, Test participants first. Pure, so the read and the write-back reply build the same response.
/// </summary>
internal static class PresetBuilder
{
    /// <summary><see cref="AudienceVariablesDto.VariablePresets"/> is null when the form has no Properties.</summary>
    public static AudienceVariablesDto Assemble(FormAudience audience)
    {
        VariablePresetsDto? presets = audience.Properties.Count == 0 ? null : VariablePresets(audience);
        return new AudienceVariablesDto(
            presets,
            Coverage(audience),
            presets is null ? PresetEditing.None : PresetEditing.TestParticipantsOnly,
            ReadOnly(audience, presets));
    }

    /// <summary>The real participants among the presets, in list order: the Creator locks their presets.</summary>
    private static List<string> ReadOnly(FormAudience audience, VariablePresetsDto? presets)
    {
        HashSet<string> real = audience.Presets
            .Where(source => !source.IsTest)
            .Select(source => source.Identifier)
            .ToHashSet(StringComparer.Ordinal);
        return presets?.Presets.Select(preset => preset.Name).Where(real.Contains).ToList() ?? [];
    }

    /// <summary>One entry per Property in tab order; a Property nobody filled in counts zero.</summary>
    private static CoverageDto Coverage(FormAudience audience) =>
        new(
            audience.Participants,
            audience.Properties
                .Select(property => new PropertyCoverage(
                    property.VariableName,
                    audience.Filled.GetValueOrDefault((property.Id, true)),
                    audience.Filled.GetValueOrDefault((property.Id, false))))
                .ToList());

    /// <summary>
    /// Named by identifier and described by kind; one variable per live cell, typed as the respondent's Snapshot types it.
    /// </summary>
    public static VariablePresetDto Build(PresetSource source, IReadOnlyList<Property> properties)
    {
        JsonObject variables = new();
        foreach (Property property in properties)
        {
            if (source.Cells.TryGetValue(property.Id, out string? value)
                && AudienceValueJson.Typed(property.DataType, value) is JsonNode typed)
            {
                variables[property.VariableName] = typed;
            }
        }

        return new VariablePresetDto(
            source.Identifier, variables, source.IsTest ? TestParticipantDescription : ParticipantDescription);
    }

    /// <summary>
    /// Test participants, then real ones, each by identifier, ignoring case and then by case (an external id match key
    /// is case-sensitive), so the selector reads in the same order on both providers. <see
    /// cref="VariablePresetsDto.TotalPresets"/> counts every participant on the form.
    /// </summary>
    private static VariablePresetsDto VariablePresets(FormAudience audience) =>
        new(
            VariableDefinition.Build(audience.Properties),
            audience.Presets
                .OrderByDescending(source => source.IsTest)
                .ThenBy(source => source.Identifier, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.Identifier, StringComparer.Ordinal)
                .Select(source => Build(source, audience.Properties))
                .ToList(),
            audience.Participants.Test + audience.Participants.Real);

    private const string TestParticipantDescription = "Test participant";
    private const string ParticipantDescription = "Participant";
}

/// <summary>
/// A form's Properties in tab order, the live cells of its first participants (Test participants first), how many test
/// and real participants it has, and how many of each have a live cell per Property (absent when none).
/// </summary>
internal sealed record FormAudience(
    IReadOnlyList<Property> Properties,
    IReadOnlyList<PresetSource> Presets,
    ParticipantCounts Participants,
    IReadOnlyDictionary<(long PropertyId, bool IsTest), int> Filled);

/// <summary>A form's Properties in tab order and one participant on it, real or test.</summary>
internal sealed record ParticipantAudience(IReadOnlyList<Property> Properties, PresetSource Participant);

/// <summary>A participant's identifier, whether they are a Test participant, and their live cells by property id.</summary>
internal sealed record PresetSource(string Identifier, IReadOnlyDictionary<long, string> Cells, bool IsTest);
