using System.Text.Json.Nodes;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// Reads a form's Audience variables for the designer: its Properties as a SurveyJS variable definition, and one
/// preset per participant, Test participants first.
/// </summary>
public sealed record GetAudienceVariablesQuery(long TenantId, long FormId) : IQuery<Result<AudienceVariablesDto>>;

/// <summary>
/// A form's variables. <see cref="VariablePresets"/> is null when the form has no Properties.
/// <see cref="ReadOnlyPresets"/> names the presets of real participants, which the designer may not edit.
/// </summary>
public sealed record AudienceVariablesDto(
    VariablePresetsDto? VariablePresets,
    CoverageDto Coverage,
    PresetEditing Editing,
    IReadOnlyList<string> ReadOnlyPresets);

/// <summary>
/// SurveyJS <c>ISurveyVariablePresets</c>. <see cref="Definition"/> is survey JSON, opaque to every caller but
/// survey-core. <see cref="Presets"/> holds at most <see cref="AudienceVariablesReader.PresetLimit"/>;
/// <see cref="TotalPresets"/> counts every participant on the form.
/// </summary>
public sealed record VariablePresetsDto(
    JsonObject Definition,
    IReadOnlyList<VariablePresetDto> Presets,
    int TotalPresets);

/// <summary>
/// A participant's values. <see cref="Name"/> is their identifier, unique on the form; <see cref="Description"/> says
/// which kind of participant they are, and is ignored on a save.
/// </summary>
public sealed record VariablePresetDto(string Name, JsonObject Variables, string? Description = null);

/// <summary>How many participants the form has, and how many of them have a value for each Property.</summary>
public sealed record CoverageDto(ParticipantCounts Participants, IReadOnlyList<PropertyCoverage> Properties);

/// <summary>Test and real participants on the form.</summary>
public sealed record ParticipantCounts(int Test, int Real);

/// <summary>How many test and real participants have a value for one Property.</summary>
public sealed record PropertyCoverage(string VariableName, int FilledTest, int FilledReal);

/// <summary>What the designer's preset editor may change.</summary>
public sealed record PresetEditing(bool AllowAdd, bool AllowEdit, bool AllowDelete)
{
    /// <summary>The editor adds, edits and deletes nothing: the form has no Properties.</summary>
    public static readonly PresetEditing None = new(false, false, false);

    /// <summary>
    /// The editor adds and edits Test participants and deletes none; real participants in the list are read-only.
    /// </summary>
    public static readonly PresetEditing TestParticipantsOnly = new(true, true, false);
}

internal sealed class GetAudienceVariablesHandler(IRepository<Form> forms, AudienceVariablesReader reader)
    : IQueryHandler<GetAudienceVariablesQuery, Result<AudienceVariablesDto>>
{
    public async Task<Result<AudienceVariablesDto>> Handle(
        GetAudienceVariablesQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<AudienceVariablesDto>();
        }

        FormAudience audience = await reader.ReadAsync(
            new FormKey(request.TenantId, request.FormId), cancellationToken);
        return Result.Success(PresetBuilder.Assemble(audience));
    }
}
