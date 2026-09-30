using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Personalization.Contracts;
using Endatix.Modules.Personalization.Features.Settings;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Personalization.Endpoints.Audience.Settings;

/// <summary>
/// Updates the tenant audience match key.
/// </summary>
public sealed class Update(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<UpdateAudienceSettingsRequest, Results<Ok<AudienceSettingsResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Put("audience/settings");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Update audience match key";
            summary.Description =
                "Sets the tenant-wide audience identifier kind. Refused once any audience member exists.";
            summary.Responses[200] = "Settings updated.";
            summary.Responses[400] = "Invalid identifier kind.";
            summary.Responses[409] = "Members already exist.";
        });
        Description(builder => builder
            .Produces<AudienceSettingsResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(409));
    }

    public override async Task<Results<Ok<AudienceSettingsResponse>, ProblemHttpResult>> ExecuteAsync(
        UpdateAudienceSettingsRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateAudienceSettingsCommand(tenantContext.TenantId, request.IdentifierKind!),
            ct);

        return TypedResultsBuilder
            .MapResult(result, dto => new AudienceSettingsResponse { IdentifierKind = dto.IdentifierKind })
            .SetTypedResults<Ok<AudienceSettingsResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validator for update audience settings.
/// </summary>
public sealed class UpdateAudienceSettingsValidator : Validator<UpdateAudienceSettingsRequest>
{
    public UpdateAudienceSettingsValidator()
    {
        RuleFor(request => request.IdentifierKind)
            .NotEmpty()
            .Must(AudienceIdentifierKindCodes.IsKnown!)
            .WithMessage("Identifier kind must be email or external_id.");
    }
}

/// <summary>
/// Request to update audience settings.
/// </summary>
public sealed class UpdateAudienceSettingsRequest
{
    public string? IdentifierKind { get; init; }
}
