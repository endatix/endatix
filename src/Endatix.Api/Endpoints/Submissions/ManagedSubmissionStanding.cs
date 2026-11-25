using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Api.Endpoints.Submissions;

internal interface IManagedSubmissionTarget
{
    long FormId { get; }
    long SubmissionId { get; }
    string MissingMessage => "Submission not found";

    Task<Result?> CheckAsync(ManagedSubmissionAccess access, CancellationToken ct) =>
        access.SubmissionAsync((FormId, SubmissionId), MissingMessage, ct);
}

internal sealed class ManagedSubmissionStanding<TRequest> : IPreProcessor<TRequest>
    where TRequest : class, IManagedSubmissionTarget
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        if (context.HasValidationFailures || context.Request is not { } request)
        {
            return;
        }
        var access = context.HttpContext.RequestServices.GetRequiredService<ManagedSubmissionAccess>();
        if (await request.CheckAsync(access, ct) is not { } refusal)
        {
            return;
        }
        if (request is GetSubmissionFilesRequest && refusal.IsNotFound())
        {
            await context.HttpContext.Response.SendNotFoundAsync(ct);
            return;
        }
        await context.HttpContext.Response.SendResultAsync(refusal.ToProblem());
    }
}
