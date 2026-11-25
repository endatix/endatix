using Endatix.Core.Abstractions.Authorization;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Infrastructure;

/// <summary>For endpoints whose framework policy is the final ordinary access decision.</summary>
internal sealed class AuthorizedAccountStanding<TRequest> : IPreProcessor<TRequest> where TRequest : notnull
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        if (context.HasValidationFailures)
        {
            return;
        }

        var standing = context.HttpContext.RequestServices.GetRequiredService<AccountStanding>();
        if (await standing.RefusalAsync(ct) is { } refusal)
        {
            await context.HttpContext.Response.SendResultAsync(refusal.ToProblem());
        }
    }
}
