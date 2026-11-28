using System.Diagnostics.CodeAnalysis;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Checks the current editable survey without saving or publishing it.</summary>
[Experimental("ENDATIX0007")]
public sealed class Checks(IMediator mediator) : Endpoint<DraftRequest, Results<Ok<FormCheckReport>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/draft/checks");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Check a form draft before publication";
            s.Responses[200] = "Findings for the returned draft version and content hash.";
            s.Responses[400] = "Invalid form ID.";
            s.Responses[404] = "Form not found.";
            s.Responses[503] = "Draft upgrade pending or mandatory checks unavailable.";
        });
        Description(b => b.Produces<FormCheckReport>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(503));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormCheckReport>, ProblemHttpResult>> ExecuteAsync(DraftRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var result = await mediator.Send(new CheckDraft(req.FormId), ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
