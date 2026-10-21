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
    public VariablePresetsDto? VariablePresets { get; init; }

    /// <summary>How many participants have a value for each Property (empty for now).</summary>
    public required CoverageDto Coverage { get; init; }

    /// <summary>What the designer's preset editor may change (nothing for now).</summary>
    public required PresetEditing Editing { get; init; }

    internal static AudienceVariablesResponse FromDto(AudienceVariablesDto dto) => new()
    {
        VariablePresets = dto.VariablePresets,
        Coverage = dto.Coverage,
        Editing = dto.Editing,
    };
}
