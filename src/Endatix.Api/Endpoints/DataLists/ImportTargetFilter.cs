using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Endpoints.DataLists;

// FastEndpoints binds the entire import payload before its preprocessors run.
internal sealed class ImportTargetFilter : IEndpointFilter
{
    private static readonly RequestBinder<ImportDataListRequest> _routeBinder = new(BindingSource.RouteValues);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        return await CheckTargetAsync(http) is { } problem ? problem : await next(context);
    }

    private static async Task<ProblemHttpResult?> CheckTargetAsync(HttpContext http)
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
        var access = http.RequestServices.GetRequiredService<DataListVisibility>();
        return await access.ContainsAsync(target.DataListId, http.RequestAborted)
            ? null : Result.NotFound("Data list not found.").ToProblem();
    }

    private static ProblemHttpResult InvalidRoute(HttpContext http, List<ValidationFailure> errors) =>
        TypedResults.Problem(EndatixProblemDetails.FromValidationFailures(errors, http, StatusCodes.Status400BadRequest));
}
