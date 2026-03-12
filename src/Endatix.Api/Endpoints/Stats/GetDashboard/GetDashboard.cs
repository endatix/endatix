using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Stats.GetDashboard;
using Endatix.Core.UseCases.Stats.Models;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Stats.GetDashboard;

public class GetDashboard(IMediator mediator, ITenantContext tenantContext) : EndpointWithoutRequest<Results<Ok<StorageDashboardModel>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("/admin/storage");
        Policies(SystemRole.PlatformAdmin.Name);
        Summary(s =>
        {
            s.Summary = "Get storage statistics dashboard";
            s.Description = "Returns a comprehensive view of database storage usage for the current tenant.";
        });
    }

    public override async Task<Results<Ok<StorageDashboardModel>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;
        
        var query = new GetStorageDashboardQuery(tenantId == 0 ? null : tenantId);
        var result = await mediator.Send(query, ct);


        return TypedResultsBuilder.FromResult(result)
        .SetTypedResults<Ok<StorageDashboardModel>, ProblemHttpResult>();
    }
}
