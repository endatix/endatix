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
        return await CheckTargetAsync(http) is { } problem ? problem : await next(context);
    }

    private static async Task<IResult?> CheckTargetAsync(HttpContext http)
    {
        List<ValidationFailure> errors = [];
        ImportDataListRequest target;
        try
        {
            target = await _routeBinder.BindAsync(new BinderContext(http, errors, null!, false, []), http.RequestAborted);
        }
        catch (ValidationFailureException) when (errors.Count > 0)
        {
            return InvalidRoute(http, errors);
        }
        if (target.DataListId <= 0)
        {
            var validation = new ImportDataListValidator().Validate(target, options => options.IncludeProperties(x => x.DataListId));
            return InvalidRoute(http, validation.Errors);
        }
        var access = http.RequestServices.GetRequiredService<ManagedDataListAccess>();
        return (await access.RefusalAsync(target.DataListId, http.RequestAborted))?.ToProblem();
    }

    private static IResult InvalidRoute(HttpContext http, List<ValidationFailure> errors) =>
        TypedResults.Problem(EndatixProblemDetails.FromValidationFailures(errors, http, StatusCodes.Status400BadRequest));
}
