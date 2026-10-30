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
                "A tenant that never set it gets email. isLocked is true while any person is on a form's audience. " +
                "importLimits says how large a CSV this host imports, so the limits can be stated before a file is chosen.";
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

    /// <summary>How large a CSV this host imports.</summary>
    public AudienceImportLimitsResponse ImportLimits { get; init; } = new();

    internal static AudienceSettingsResponse FromDto(AudienceSettingsDto dto) => new()
    {
        IdentifierKind = dto.IdentifierKind,
        IsLocked = dto.IsLocked,
        ImportLimits = new AudienceImportLimitsResponse
        {
            MaxRows = dto.ImportLimits.MaxRows,
            MaxBytes = dto.ImportLimits.MaxBytes,
            RunsInBackground = dto.ImportLimits.RunsInBackground,
        },
    };
}

/// <summary>
/// How large a CSV this host imports: larger where imports run as background jobs than where they run in a request.
/// </summary>
public sealed class AudienceImportLimitsResponse
{
    /// <summary>Most data rows in one file.</summary>
    public int MaxRows { get; init; }

    /// <summary>Largest file, in bytes.</summary>
    public long MaxBytes { get; init; }

    /// <summary>Whether imports run in the background, so the scripter may close the page.</summary>
    public bool RunsInBackground { get; init; }
}
