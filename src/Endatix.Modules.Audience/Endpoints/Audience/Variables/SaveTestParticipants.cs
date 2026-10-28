using System.Text.Json.Nodes;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.Variables;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Variables;

/// <summary>
/// Saves the form designer's edited presets as Test participants.
/// </summary>
public sealed class SaveTestParticipants(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<SaveTestParticipantsRequest, Results<Ok<AudienceVariablesResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Put("forms/{formId}/audience/variables/test-participants");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Save test participant values";
            summary.Description = "Saves the designer's edited presets as test participants, matched by identifier; " +
                "a replay writes nothing, a change to a real participant refuses it, nothing is deleted. Returns the variables read.";
            summary.Responses[200] = "The form's variables after the save.";
            summary.Responses[400] = "Invalid request, identifier or value, a repeated participant or a changed real one.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] = "A parallel request added the same participant first.";
        });
        Description(builder => builder
            .Produces<AudienceVariablesResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AudienceVariablesResponse>, ProblemHttpResult>> ExecuteAsync(
        SaveTestParticipantsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new SaveTestParticipantValuesCommand(
                tenantContext.TenantId,
                request.FormId,
                request.Presets!.Select(preset => new VariablePresetDto(preset.Name!, preset.Variables!)).ToList()),
            ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceVariablesResponse.FromDto)
            .SetTypedResults<Ok<AudienceVariablesResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for save test participant values.
/// </summary>
public sealed class SaveTestParticipantsValidator : Validator<SaveTestParticipantsRequest>
{
    /// <summary>The most presets one request saves; the designer holds the 20 the variables read returns, plus new ones.</summary>
    public const int MaxPresets = 100;

    /// <summary>A positive form id and up to <see cref="MaxPresets"/> presets, each named and with variables.</summary>
    public SaveTestParticipantsValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Presets).NotNull();
        RuleFor(request => request.Presets!.Count).LessThanOrEqualTo(MaxPresets).When(request => request.Presets is not null);
        RuleForEach(request => request.Presets).NotNull();
        RuleForEach(request => request.Presets).ChildRules(preset =>
        {
            preset.RuleFor(item => item.Name).NotEmpty();
            preset.RuleFor(item => item.Variables).NotNull();
            preset.RuleFor(item => item.Variables)
                .Must(HaveDistinctNames)
                .WithMessage("Each variable can appear once in a preset.");
        });
    }

    /// <summary>
    /// A <see cref="JsonObject"/> bound from a body that repeats a name throws when first read, so it is read here, where
    /// the repeat is a 400.
    /// </summary>
    private static bool HaveDistinctNames(JsonObject? variables)
    {
        try
        {
            return variables is null || variables.Count >= 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Request to save test participant values.
/// </summary>
public sealed class SaveTestParticipantsRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The designer's whole edited preset list.</summary>
    public List<SaveTestParticipantPreset>? Presets { get; init; }
}

/// <summary>One edited preset, in SurveyJS's preset shape.</summary>
public sealed class SaveTestParticipantPreset
{
    /// <summary>The test participant's identifier.</summary>
    public string? Name { get; init; }

    /// <summary>Variable name → value, as the variables read types it.</summary>
    public JsonObject? Variables { get; init; }
}
