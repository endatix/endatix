using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.TenantSettings.Get;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.TenantSettings;

/// <summary>
/// Endpoint for getting tenant settings for the current authenticated user's tenant.
/// Sensitive data (tokens, API keys, URLs) are masked for security.
/// </summary>
public class GetTenantSettings(IMediator mediator) : EndpointWithoutRequest<Results<Ok<TenantSettingsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("tenant-settings");
        Permissions(Actions.Access.Hub);
        Summary(s =>
        {
            s.Summary = "Get tenant settings for the current tenant";
            s.Description = "Retrieves tenant configuration settings including.";
            s.Responses[200] = "Tenant settings retrieved successfully.";
            s.Responses[400] = "Invalid request or tenant context.";
            s.Responses[404] = "Tenant settings not found.";
        });
        Description(builder => builder
            .Produces<TenantSettingsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<TenantSettingsModel>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new GetTenantSettingsQuery(), ct);

        return TypedResultsBuilder
            .MapResult(result, TenantSettingsMapper.Map)
            .SetTypedResults<Ok<TenantSettingsModel>, ProblemHttpResult>();
    }
}
