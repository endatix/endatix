using System.Globalization;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Validates the draft precondition and returns the winning draft on a conflict.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public abstract class DraftWriteEndpoint<TRequest, TResponse>(IMediator mediator)
    : Endpoint<TRequest, Results<Ok<TResponse>, ProblemHttpResult>> where TRequest : DraftRequest
{
    /// <summary>The draft route's HTTP method.</summary>
    protected abstract Http Verb { get; }
    /// <summary>Builds the write using the version from If-Match.</summary>
    protected abstract WriteDraft Command(TRequest request, long version);
    /// <summary>Maps a successful save to the operation's wire response.</summary>
    protected abstract TResponse Map(TRequest request, DraftResponse draft);

    /// <inheritdoc/>
    public sealed override void Configure()
    {
        Verbs(Verb);
        Routes("forms/{formId}/draft");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = $"{Verb} a form draft";
            s.Responses[200] = "Draft saved. The ETag identifies the current draft.";
            s.Responses[400] = "Invalid draft request or If-Match.";
            s.Responses[404] = "Form not found.";
            s.Responses[412] = "The form changed elsewhere. The response includes the current draft and ETag.";
            s.Responses[428] = "Send If-Match with the ETag from the last draft read.";
            s.Responses[503] = "This form's draft upgrade is pending.";
        });
        Description(b => b.Produces<TResponse>().ProducesProblem(400).ProducesProblem(404)
            .ProducesProblem(412).ProducesProblem(428).ProducesProblem(503));
    }

    /// <inheritdoc/>
    public sealed override async Task<Results<Ok<TResponse>, ProblemHttpResult>> ExecuteAsync(TRequest req, CancellationToken ct)
    {
        if (EntityTagHeader.ReadIfMatch(HttpContext.Request.Headers.IfMatch.ToString(), out var version) is { } problem)
        {
            return problem;
        }
        var result = await mediator.Send(Command(req, version), ct);
        if (!result.IsSuccess)
        {
            return result.Status == ResultStatus.Conflict ? await StaleAsync(req.FormId, ct) : result.ToProblem();
        }
        EntityTagHeader.SetETag(HttpContext.Response, result.Value.Version);
        return TypedResults.Ok(Map(req, result.Value));
    }

    private async Task<ProblemHttpResult> StaleAsync(long id, CancellationToken ct)
    {
        var current = await mediator.Send(new ReadDraft(id), ct);
        if (!current.IsSuccess)
        {
            return current.ToProblem();
        }
        var problem = EndatixProblemDetails.Create(412, "Precondition failed", "This form changed elsewhere.");
        problem.Extensions["version"] = current.Value.Version.ToString(CultureInfo.InvariantCulture);
        problem.Extensions["draft"] = current.Value;
        EntityTagHeader.SetETag(HttpContext.Response, current.Value.Version);
        return TypedResults.Problem(problem);
    }
}
