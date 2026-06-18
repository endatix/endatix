using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.PlatformAdmins;

/// <summary>
/// Endpoint for granting platform administrator access.
/// </summary>
public sealed class Grant(IMediator mediator)
    : Endpoint<GrantPlatformAdminRequest, Results<Ok<PlatformAdminOperation>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("/admin/platform-admins/{userId}");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Grant platform administrator";
            s.Description = "Locally approves a user as a platform administrator.";
            s.Responses[200] = "Platform administrator access granted.";
            s.Responses[400] = "Invalid request or role mutation failed.";
            s.Responses[403] = "The current user is not allowed to grant platform administrator access.";
            s.Responses[404] = "User not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<PlatformAdminOperation>, ProblemHttpResult>> ExecuteAsync(
        GrantPlatformAdminRequest request,
        CancellationToken ct)
    {
        var command = new GrantPlatformAdminCommand(request.UserId);
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, PlatformAdminOperation.Success)
            .SetTypedResults<Ok<PlatformAdminOperation>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for granting platform administrator access.
/// </summary>
public sealed record GrantPlatformAdminRequest
{
    public long UserId { get; init; }
}

/// <summary>
/// Validator for granting platform administrator access.
/// </summary>
public sealed class GrantPlatformAdminValidator : Validator<GrantPlatformAdminRequest>
{
    public GrantPlatformAdminValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0);
    }
}
