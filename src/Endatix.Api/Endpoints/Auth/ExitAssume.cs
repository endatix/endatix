using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Identity.ExitAssume;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Returns a PlatformAdmin from an assumed tenant to their home tenant.
/// </summary>
[Endatix.Core.Infrastructure.Attributes.AllowWithRequiredActions]
public sealed class ExitAssume(IMediator mediator, IConfiguration configuration)
    : EndpointWithoutRequest<Results<Ok<TenantSessionResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
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
            s.Responses[403] = "Platform administrator access and a verified assumed session are required.";
            s.Responses[404] = "Multi-tenancy is not enabled on this deployment.";
            s.Responses[500] = "The session could not be persisted.";
        });
        Description(builder => builder.WithMetadata(new ReturnHomeSessionMetadata()).Produces<TenantSessionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<TenantSessionResponse>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        if (MultiTenancyGate.Refusal(configuration) is { } refusal)
        {
            return refusal;
        }

        var result = await mediator.Send(new ExitAssumeCommand(), ct);

        return TypedResultsBuilder
            .MapResult(result, TenantSessionResponse.Map)
            .SetTypedResults<Ok<TenantSessionResponse>, ProblemHttpResult>();
    }
}
