using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.DataLists;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Endpoints.DataLists;

internal interface IManagedDataListTarget
{
    long DataListId { get; }
}

internal sealed class ManagedDataListStanding<TRequest> : IPreProcessor<TRequest>
    where TRequest : class, IManagedDataListTarget
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        if (context.HasValidationFailures || context.Request is not { } request)
        {
            return;
        }
        var access = context.HttpContext.RequestServices.GetRequiredService<ManagedDataListAccess>();
        if (await access.RefusalAsync(request.DataListId, ct) is { } refusal)
        {
            await context.HttpContext.Response.SendResultAsync(refusal.ToProblem());
        }
    }
}

// FastEndpoints binds the entire import payload before its preprocessors run.
internal sealed class ImportStandingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (long.TryParse(http.Request.RouteValues["dataListId"]?.ToString(), out var id) && id > 0)
        {
            var access = http.RequestServices.GetRequiredService<ManagedDataListAccess>();
            if (await access.RefusalAsync(id, http.RequestAborted) is { } refusal)
            {
                return refusal.ToProblem();
            }
        }
        return await next(context);
    }
}
