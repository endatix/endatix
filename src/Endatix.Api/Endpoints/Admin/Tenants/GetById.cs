using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Tenants.GetById;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Admin.Tenants;

/// <summary>
/// Endpoint for loading a platform tenant by id.
/// </summary>
public sealed class GetById(IMediator mediator, IConfiguration configuration)
    : Endpoint<GetTenantByIdRequest, Results<Ok<TenantModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}");
        Policies(AuthorizationPolicies.PlatformAdminAccess);
        Summary(s =>
        {
            s.Summary = "Get tenant";
            s.Description = "Returns a tenant and its self-registration policy.";
            s.ExampleRequest = new GetTenantByIdRequest { TenantId = 1 };
            s.ResponseExamples[200] = TenantModel.Example;
            s.Responses[200] = "Tenant retrieved successfully.";
            s.Responses[403] = "The current user is not a platform administrator.";
            s.Responses[404] = "Tenant not found, or multi-tenancy is not enabled on this deployment.";
        });
        Description(builder => builder
            .Produces<TenantModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<TenantModel>, ProblemHttpResult>> ExecuteAsync(
        GetTenantByIdRequest request,
        CancellationToken ct)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<TenantModel>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<TenantModel>, ProblemHttpResult>();
        }

        GetTenantByIdQuery query = new(request.TenantId);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, TenantModel.Map)
            .SetTypedResults<Ok<TenantModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request for loading a platform tenant by id.
/// </summary>
public sealed class GetTenantByIdRequest
{
    /// <summary>
    /// The tenant to load.
    /// </summary>
    public long TenantId { get; set; }
}

/// <summary>
/// Validator for <c>GetTenantByIdRequest</c>.
/// </summary>
public sealed class GetTenantByIdValidator : Validator<GetTenantByIdRequest>
{
    public GetTenantByIdValidator()
    {
        RuleFor(request => request.TenantId)
            .GreaterThan(0);
    }
}
