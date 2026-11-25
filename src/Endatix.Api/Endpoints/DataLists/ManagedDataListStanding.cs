using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
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
    private static readonly RequestBinder<ImportDataListRequest> _routeBinder = new(BindingSource.RouteValues);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!http.RequestServices.GetRequiredService<AccountStanding>().IsEnforced)
        {
            return await next(context);
        }
        List<ValidationFailure> errors = [];
        var target = await _routeBinder.BindAsync(new BinderContext(http, errors, null!, false, []), http.RequestAborted);
        if (target.DataListId <= 0 || errors.Count > 0)
        {
            var validation = new ImportDataListValidator().Validate(target, options => options.IncludeProperties(x => x.DataListId));
            errors.AddRange(validation.Errors);
            return TypedResults.Problem(EndatixProblemDetails.FromValidationFailures(errors, http, StatusCodes.Status400BadRequest));
        }
        var access = http.RequestServices.GetRequiredService<ManagedDataListAccess>();
        return await access.RefusalAsync(target.DataListId, http.RequestAborted) is { } refusal
            ? refusal.ToProblem() : await next(context);
    }
}
