using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.GetDashboard;
using Endatix.Core.UseCases.Stats.Models;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Stats.GetDashboard;

public class GetDashboard(IMediator mediator, ITenantContext tenantContext) : EndpointWithoutRequest<Results<Ok<StorageDashboardModel>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/storage");
        Permissions(Actions.Tenant.ViewSettings);
        Summary(s =>
        {
            s.Summary = "Get storage statistics dashboard";
            s.Description = "Returns a comprehensive view of database storage usage for the current tenant.";
            s.DescribeStandingProblems();
        });
        Description(builder => builder.ProducesStandingProblems()
            .Produces<StorageDashboardModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    public override async Task<Results<Ok<StorageDashboardModel>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId <= 0)
        {
            return Result.Forbidden("Tenant context is required.").ToProblem();
        }

        var result = await mediator.Send(new GetStorageDashboardQuery(tenantId), ct);
        return TypedResultsBuilder.FromResult(result)
            .SetTypedResults<Ok<StorageDashboardModel>, ProblemHttpResult>();
    }
}
