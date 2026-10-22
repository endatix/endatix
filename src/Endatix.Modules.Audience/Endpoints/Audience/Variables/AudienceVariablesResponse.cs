using System.Text.Json.Nodes;
using Endatix.Modules.Audience.Features.Variables;

namespace Endatix.Modules.Audience.Endpoints.Audience.Variables;

/// <summary>
/// Wire model for a form's Audience variables, in the shape the form designer reads.
/// </summary>
public sealed class AudienceVariablesResponse
{
    /// <summary>
    /// SurveyJS variable presets: a definition with one question per Property, and one preset per Test participant
    /// named by identifier. <see langword="null"/> when the form has no Properties.
    /// </summary>
    public AudienceVariablePresetsResponse? VariablePresets { get; init; }

    /// <summary>How many participants have a value for each Property (empty for now).</summary>
    public required AudienceVariableCoverageResponse Coverage { get; init; }

    /// <summary>What the designer's preset editor may change (nothing for now).</summary>
    public required AudiencePresetEditingResponse Editing { get; init; }

    internal static AudienceVariablesResponse FromDto(AudienceVariablesDto dto) => new()
    {
        VariablePresets = dto.VariablePresets is { } presets ? AudienceVariablePresetsResponse.From(presets) : null,
        Coverage = AudienceVariableCoverageResponse.From(dto.Coverage),
        Editing = AudiencePresetEditingResponse.From(dto.Editing),
    };
}

/// <summary>SurveyJS <c>ISurveyVariablePresets</c>.</summary>
/// <param name="Definition">Survey JSON whose top-level questions are the variables.</param>
/// <param name="Presets">The first 20 Test participants by when they were added, ordered by identifier.</param>
/// <param name="TotalPresets">How many Test participants the form has; more than <paramref name="Presets"/> holds when
/// it has over 20.</param>
public sealed record AudienceVariablePresetsResponse(
    JsonObject Definition,
    IReadOnlyList<AudienceVariablePresetResponse> Presets,
    int TotalPresets)
{
    internal static AudienceVariablePresetsResponse From(VariablePresetsDto dto) =>
        new(
            dto.Definition,
            dto.Presets.Select(AudienceVariablePresetResponse.From).ToList(),
            dto.TotalPresets);
}

/// <summary>One participant's values.</summary>
/// <param name="Name">The participant's identifier.</param>
/// <param name="Variables">Variable name → value, typed as the respondent's <c>personalization</c>.</param>
public sealed record AudienceVariablePresetResponse(string Name, JsonObject Variables)
{
    internal static AudienceVariablePresetResponse From(VariablePresetDto dto) => new(dto.Name, dto.Variables);
}

/// <summary>How many participants have a value for each Property.</summary>
/// <param name="Participants">Test and real participants on the form.</param>
/// <param name="Properties">Per Property, how many test and real participants have a value.</param>
public sealed record AudienceVariableCoverageResponse(
    AudienceParticipantCountsResponse Participants,
    IReadOnlyList<AudiencePropertyCoverageResponse> Properties)
{
    internal static AudienceVariableCoverageResponse From(CoverageDto dto) =>
        new(
            new AudienceParticipantCountsResponse(dto.Participants.Test, dto.Participants.Real),
            dto.Properties
                .Select(property => new AudiencePropertyCoverageResponse(
                    property.VariableName, property.FilledTest, property.FilledReal))
                .ToList());
}

/// <summary>Test and real participants on the form.</summary>
/// <param name="Test">Test participants.</param>
/// <param name="Real">Real participants.</param>
public sealed record AudienceParticipantCountsResponse(int Test, int Real);

/// <summary>How many test and real participants have a value for one Property.</summary>
/// <param name="VariableName">The Property's variable name.</param>
/// <param name="FilledTest">Test participants with a value.</param>
/// <param name="FilledReal">Real participants with a value.</param>
public sealed record AudiencePropertyCoverageResponse(string VariableName, int FilledTest, int FilledReal);

/// <summary>What the designer's preset editor may change.</summary>
/// <param name="AllowAdd">Add a Test participant.</param>
/// <param name="AllowEdit">Edit a Test participant's values.</param>
/// <param name="AllowDelete">Delete a preset.</param>
public sealed record AudiencePresetEditingResponse(bool AllowAdd, bool AllowEdit, bool AllowDelete)
{
    internal static AudiencePresetEditingResponse From(PresetEditing dto) =>
        new(dto.AllowAdd, dto.AllowEdit, dto.AllowDelete);
}
