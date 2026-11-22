using Endatix.Api.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.TenantBranding;

/// <summary>
/// Answers a body over the endpoint's request size limit with 413. The server refuses it while the body is bound, and
/// the exception handler would otherwise answer that client error with a 500.
/// </summary>
internal sealed class RequestBodyTooLargeFilter : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            // FastEndpoints' handler binds and writes the response itself and hands back that work as a Task, which
            // the route would await after this filter; await it here so the bind's exception is caught.
            var result = await next(context);
            if (result is Task work)
            {
                await work;
                return Results.Empty;
            }

            return result;
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TypedResults.Problem(EndatixProblemDetails.Create(StatusCodes.Status413PayloadTooLarge,
                "Payload too large", "The request body is larger than this endpoint accepts."));
        }
    }
}
