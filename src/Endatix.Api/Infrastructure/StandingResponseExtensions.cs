using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Infrastructure;

internal static class StandingResponseExtensions
{
    internal static RouteHandlerBuilder ProducesStandingProblems(this RouteHandlerBuilder builder) =>
        builder.ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
