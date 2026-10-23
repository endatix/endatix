using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using MediatR;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// Saves the designer's edited presets as Test participants, matched by identifier (the preset name), and returns the
/// variables read again. Idempotent: the same request twice writes once.
/// </summary>
public sealed record SaveTestParticipantValuesCommand(long TenantId, long FormId, IReadOnlyList<VariablePresetDto> Presets)
    : ICommand<Result<AudienceVariablesDto>>;

internal sealed class SaveTestParticipantValuesHandler(
    IMediator mediator,
    IRepository<Form> forms,
    AudienceVariablesReader reader)
    : ICommandHandler<SaveTestParticipantValuesCommand, Result<AudienceVariablesDto>>
{
    /// <summary>
    /// Everything is checked before the first write: the form, each identifier against the match key, then the plan.
    /// Writes go through the person commands, so their rules, locks and races apply; presets left out of the request
    /// are left alone.
    /// </summary>
    public async Task<Result<AudienceVariablesDto>> Handle(
        SaveTestParticipantValuesCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<AudienceVariablesDto>();
        }

        FormKey key = new(request.TenantId, request.FormId);
        Result<SavePlan> plan = await PlanAsync(key, request.Presets, cancellationToken);
        IResult? failure = plan.IsSuccess ? await WriteAsync(key, plan.Value.Writes, cancellationToken) : plan;
        return failure is null
            ? Result.Success(PresetBuilder.Assemble(await reader.ReadAsync(key, plan.Value.Keys, cancellationToken)))
            : failure.ToErrorResult<AudienceVariablesDto>();
    }

    /// <summary>The writes, and the normalized identifiers of every preset saved, so the reply holds them all.</summary>
    private async Task<Result<SavePlan>> PlanAsync(
        FormKey key,
        IReadOnlyList<VariablePresetDto> presets,
        CancellationToken cancellationToken)
    {
        Result<KeyedAudience> read = await reader.ReadByIdentifiersAsync(
            key, presets.Select(preset => preset.Name).ToList(), cancellationToken);
        if (!read.IsSuccess)
        {
            return read.ToErrorResult<SavePlan>();
        }

        KeyedAudience audience = read.Value;
        Result<IReadOnlyList<PresetWrite>> writes =
            PresetReconciler.Plan(audience.Properties, audience.OnForm, Edits(presets, audience.Keys));
        return writes.IsSuccess
            ? Result.Success(new SavePlan(writes.Value, audience.Keys.Values.ToHashSet()))
            : writes.ToErrorResult<SavePlan>();
    }

    private static List<PresetEdit> Edits(
        IReadOnlyList<VariablePresetDto> presets,
        IReadOnlyDictionary<string, string> keys) =>
        presets.Select(preset => new PresetEdit(preset.Name, keys[preset.Name], preset.Variables)).ToList();

    /// <summary>
    /// The first write that fails, or null. The writes before it stay, and a replay finishes the rest.
    /// </summary>
    private async Task<IResult?> WriteAsync(
        FormKey key,
        IReadOnlyList<PresetWrite> writes,
        CancellationToken cancellationToken)
    {
        foreach (PresetWrite write in writes)
        {
            Result<PersonDto> written = await SendAsync(key, write, cancellationToken);
            if (!written.IsSuccess)
            {
                return written;
            }
        }

        return null;
    }

    private Task<Result<PersonDto>> SendAsync(FormKey key, PresetWrite write, CancellationToken cancellationToken) =>
        write.MembershipId is long membershipId
            ? mediator.Send(
                new UpdatePersonCommand(key.TenantId, key.FormId, membershipId, write.Values),
                cancellationToken)
            : mediator.Send(
                new CreatePersonCommand(key.TenantId, key.FormId, write.Identifier, write.Values, IsTest: true),
                cancellationToken);

    private sealed record SavePlan(IReadOnlyList<PresetWrite> Writes, IReadOnlyCollection<string> Keys);
}
