using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>The OpenAPI responses <see cref="AddSearchKey"/> and <see cref="RemoveSearchKey"/> share.</summary>
internal static class SearchKeyEndpointDocs
{
    /// <summary>Describes 200, 401, 403 and 503; each endpoint describes its own 400 and 404.</summary>
    public static void DescribeSharedResponses(EndpointSummary summary)
    {
        summary.Responses[200] = "The form's search keys after the change.";
        summary.Responses[401] = "No tenant in the request.";
        summary.Responses[403] = "The caller may not edit forms.";
        summary.Responses[503] = "The form's reporting schema is being changed; try again.";
    }

    public static RouteHandlerBuilder ProducesSearchKeys(this RouteHandlerBuilder builder) =>
        builder
            .Produces<SubmissionGridSearchKeys>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
