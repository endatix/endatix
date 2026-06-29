using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Features.Auth;
using Endatix.Core.UseCases.Admin.Auth.GetSettings;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Admin.Auth;

/// <summary>
/// Endpoint for retrieving safe API authentication settings.
/// </summary>
public sealed class GetAuthSettings(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<AuthSettingsDto>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/auth/settings");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Get auth settings";
            s.Description = "Returns configured API authentication providers and expiries without secrets.";
            s.Responses[200] = "Auth settings retrieved successfully.";
            s.Responses[400] = "Invalid request.";
        });
        Description(builder => builder
            .Produces<AuthSettingsDto>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<AuthSettingsDto>, ProblemHttpResult>> ExecuteAsync(
        CancellationToken ct)
    {
        GetAuthSettingsQuery query = new();
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder.FromResult(result)
            .SetTypedResults<Ok<AuthSettingsDto>, ProblemHttpResult>();
    }
}
