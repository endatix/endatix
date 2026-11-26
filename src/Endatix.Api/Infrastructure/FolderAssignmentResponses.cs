using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Infrastructure;

internal static class FolderAssignmentResponses
{
    internal static void DescribeFolderAssignment(this EndpointSummary summary, string notFound)
    {
        summary.Responses[400] = "Invalid input or a required folder assignment is missing.";
        summary.Responses[404] = notFound;
        summary.Responses[409] = "The target folder is inactive or the change conflicts with the current state.";
    }

    internal static RouteHandlerBuilder ProducesFolderAssignmentProblems(this RouteHandlerBuilder builder) => builder
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
}
