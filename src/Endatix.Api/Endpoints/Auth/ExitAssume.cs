using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.ExitAssume;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Returns a PlatformAdmin from an assumed tenant to their home tenant.
/// </summary>
public sealed class ExitAssume(IMediator mediator, IConfiguration configuration)
    : EndpointWithoutRequest<Results<Ok<AssumeTenantResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/exit-assume");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Exit assumed tenant";
            s.Description = "Ends the assumed-tenant session and issues tokens for the actor's home tenant.";
            s.Responses[200] = "Home-tenant session issued.";
            s.Responses[400] = "The current session is not assumed.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Multi-tenancy is not enabled on this deployment.";
        });
    }

    public override async Task<Results<Ok<AssumeTenantResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<AssumeTenantResponse>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
        }

        var result = await mediator.Send(new ExitAssumeCommand(), cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, tokens => new AssumeTenantResponse(tokens.AccessToken.Token, tokens.RefreshToken.Token))
            .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
    }
}
