using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
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
            s.Responses[400] = "Invalid tenant id, or the session is already assumed.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Multi-tenancy is disabled, or the tenant was not found.";
        });
        Description(builder => builder
            .Produces<TenantSessionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound));
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

/// <summary>
/// Request for assuming a tenant session.
/// </summary>
public sealed class AssumeTenantRequest
{
    /// <summary>
    /// The tenant to assume.
    /// </summary>
    public long TenantId { get; set; }
}

/// <summary>
/// Validator for <c>AssumeTenantRequest</c>.
/// </summary>
public sealed class AssumeTenantValidator : Validator<AssumeTenantRequest>
{
    public AssumeTenantValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
    }
}

/// <summary>
/// Tokens for the tenant session issued by assume-tenant and exit-assume.
/// </summary>
public sealed record TenantSessionResponse(string AccessToken, string RefreshToken)
{
    internal static TenantSessionResponse Map(AuthTokensDto tokens) =>
        new(tokens.AccessToken.Token, tokens.RefreshToken.Token);
}
