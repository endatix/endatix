using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// Reads one participant's values as a preset for Preview as: any participant on the form, real or test, by audience
/// member id.
/// </summary>
public sealed record GetParticipantPresetQuery(long TenantId, long FormId, long MemberId)
    : IQuery<Result<VariablePresetDto>>;

internal sealed class GetParticipantPresetHandler(IRepository<Form> forms, AudienceVariablesReader reader)
    : IQueryHandler<GetParticipantPresetQuery, Result<VariablePresetDto>>
{
    public async Task<Result<VariablePresetDto>> Handle(
        GetParticipantPresetQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<VariablePresetDto>();
        }

        ParticipantAudience? audience = await reader.ReadOneAsync(
            new FormKey(request.TenantId, request.FormId), request.MemberId, cancellationToken);
        return audience is null
            ? Result.NotFound("Participant not found on this form.")
            : Result.Success(PresetBuilder.Build(audience.Participant, audience.Properties));
    }
}
