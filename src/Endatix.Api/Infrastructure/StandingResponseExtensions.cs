using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Infrastructure;

internal static class StandingResponseExtensions
{
    internal static void DescribeStandingProblems(this EndpointSummary summary)
    {
        summary.Responses[StatusCodes.Status403Forbidden] = "The operation is forbidden or the account has a blocking required action.";
        summary.Responses[StatusCodes.Status503ServiceUnavailable] = "Account standing is temporarily unavailable.";
    }

    internal static RouteHandlerBuilder ProducesStandingProblems(this RouteHandlerBuilder builder) =>
        builder.ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
