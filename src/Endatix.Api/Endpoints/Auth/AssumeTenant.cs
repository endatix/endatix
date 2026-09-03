using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.AssumeTenant;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Issues a short-lived assumed-tenant session for a PlatformAdmin. Isolation stays JWT <c>tid</c>;
/// <c>act</c> marks the actor. No membership row is written.
/// </summary>
public sealed class AssumeTenant(IMediator mediator, IConfiguration configuration)
    : Endpoint<AssumeTenantRequest, Results<Ok<TenantSessionResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("auth/assume-tenant");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Assume tenant";
            s.Description = "Switches the current PlatformAdmin session into a target tenant without impersonating a user or creating membership.";
            s.ExampleRequest = new AssumeTenantRequest { TenantId = 42 };
            s.Responses[200] = "Assumed session issued.";
            s.Responses[400] = "Invalid tenant id, or the session is already assumed into a different tenant.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Multi-tenancy is disabled, or the tenant was not found.";
            s.Responses[409] = "The session is already in the target tenant.";
            s.Responses[500] = "The session could not be persisted.";
        });
        Description(builder => builder
            .Produces<TenantSessionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<TenantSessionResponse>, ProblemHttpResult>> ExecuteAsync(
        AssumeTenantRequest request,
        CancellationToken ct)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<TenantSessionResponse>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<TenantSessionResponse>, ProblemHttpResult>();
        }

        var result = await mediator.Send(new AssumeTenantCommand(request.TenantId), ct);

        return TypedResultsBuilder
            .MapResult(result, TenantSessionResponse.Map)
            .SetTypedResults<Ok<TenantSessionResponse>, ProblemHttpResult>();
    }
}

public sealed class AssumeTenantRequest
{
    public long TenantId { get; set; }
}

public sealed class AssumeTenantValidator : Validator<AssumeTenantRequest>
{
    public AssumeTenantValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
    }
}
