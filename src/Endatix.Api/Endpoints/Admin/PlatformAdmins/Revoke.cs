using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.PlatformAdmins;

/// <summary>
/// Endpoint for revoking platform administrator access.
/// </summary>
public sealed class Revoke(IMediator mediator)
    : Endpoint<RevokePlatformAdminRequest, Results<Ok<PlatformAdminOperation>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete("/admin/platform-admins/{userId}");
        Policies(SystemRole.PlatformAdmin.Name);
        Summary(s =>
        {
            s.Summary = "Revoke platform administrator";
            s.Description = "Removes a user's local platform administrator approval.";
            s.Responses[200] = "Platform administrator access revoked.";
            s.Responses[400] = "Invalid request or role mutation failed.";
            s.Responses[403] = "The current user is not allowed to revoke platform administrator access.";
            s.Responses[404] = "User not found.";
            s.Responses[409] = "Revocation would leave the platform without an active platform administrator.";
        });
    }

    public override async Task<Results<Ok<PlatformAdminOperation>, ProblemHttpResult>> ExecuteAsync(
        RevokePlatformAdminRequest request,
        CancellationToken ct)
    {
        var command = new RevokePlatformAdminCommand(request.UserId);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, PlatformAdminOperation.Success)
            .SetTypedResults<Ok<PlatformAdminOperation>, ProblemHttpResult>();
    }
}


/// <summary>
/// Request for revoking platform administrator access.
/// </summary>
public sealed record RevokePlatformAdminRequest
{
    public long UserId { get; init; }
}

/// <summary>
/// Validator for revoking platform administrator access.
/// </summary>
public sealed class RevokePlatformAdminValidator : Validator<RevokePlatformAdminRequest>
{
    public RevokePlatformAdminValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0);
    }
}
