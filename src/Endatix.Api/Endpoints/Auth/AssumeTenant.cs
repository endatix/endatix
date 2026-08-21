using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.AssumeTenant;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Issues a short-lived assumed-tenant session for a PlatformAdmin. Isolation stays JWT <c>tid</c>;
/// <c>act</c> marks the actor. No membership row is written.
/// </summary>
public sealed class AssumeTenant(IMediator mediator, IConfiguration configuration)
    : Endpoint<AssumeTenantRequest, Results<Ok<AssumeTenantResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/assume-tenant");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Assume tenant";
            s.Description = "Switches the current PlatformAdmin session into a target tenant without impersonating a user or creating membership.";
            s.Responses[200] = "Assumed session issued.";
            s.Responses[400] = "Invalid tenant id.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Multi-tenancy is disabled, or the tenant was not found.";
        });
    }

    public override async Task<Results<Ok<AssumeTenantResponse>, ProblemHttpResult>> ExecuteAsync(
        AssumeTenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<AssumeTenantResponse>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
        }

        var result = await mediator.Send(new AssumeTenantCommand(request.TenantId), cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
    }

    private static AssumeTenantResponse Map(AuthTokensDto tokens) =>
        new(tokens.AccessToken.Token, tokens.RefreshToken.Token);
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

public sealed record AssumeTenantResponse(string AccessToken, string RefreshToken);
