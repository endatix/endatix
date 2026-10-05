using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.Settings;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Settings;

/// <summary>
/// Reads the tenant audience match key.
/// </summary>
public sealed class Get(
    IMediator mediator,
    ITenantContext tenantContext)
    : EndpointWithoutRequest<Results<Ok<AudienceSettingsResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("audience/settings");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Get audience match key";
            summary.Description =
                "Returns the tenant-wide audience identifier kind (email or external_id). " +
                "A tenant that never set it gets email. isLocked is true while any person is on a form's audience.";
            summary.Responses[200] = "Settings retrieved.";
        });
        Description(builder => builder
            .Produces<AudienceSettingsResponse>(200, "application/json")
            .ProducesProblem(401));
    }

    public override async Task<Results<Ok<AudienceSettingsResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetAudienceSettingsQuery(tenantContext.TenantId), ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceSettingsResponse.FromDto)
            .SetTypedResults<Ok<AudienceSettingsResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Audience settings response.
/// </summary>
public sealed class AudienceSettingsResponse
{
    public string IdentifierKind { get; init; } = string.Empty;

    /// <summary>True while any person is on a form's audience. The match key cannot change then.</summary>
    public bool IsLocked { get; init; }

    internal static AudienceSettingsResponse FromDto(AudienceSettingsDto dto) => new()
    {
        IdentifierKind = dto.IdentifierKind,
        IsLocked = dto.IsLocked,
    };
}
