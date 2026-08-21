using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ListMyTenants;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Lists tenants the current user belongs to for the Hub switcher.
/// </summary>
public sealed class ListMyTenants(IMediator mediator, IConfiguration configuration)
    : EndpointWithoutRequest<Results<Ok<UserTenantsResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("auth/tenants");
        Summary(s =>
        {
            s.Summary = "List my tenants";
            s.Description = "Returns tenants the current user can switch into. Active is the last-used tenant.";
            s.Responses[200] = "Membership list returned.";
            s.Responses[401] = "Authentication required.";
            s.Responses[404] = "Multi-tenancy is not enabled on this deployment.";
        });
    }

    public override async Task<Results<Ok<UserTenantsResponse>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<UserTenantsResponse>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<UserTenantsResponse>, ProblemHttpResult>();
        }

        var result = await mediator.Send(new ListMyTenantsQuery(), cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, tenants => new UserTenantsResponse(
                tenants.Items
                    .Select(tenant => new UserTenantModel(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive))
                    .ToList()))
            .SetTypedResults<Ok<UserTenantsResponse>, ProblemHttpResult>();
    }
}

public sealed record UserTenantModel(long Id, string Name, string Slug, bool IsActive);

public sealed record UserTenantsResponse(IReadOnlyList<UserTenantModel> Items);
