using System.Diagnostics.CodeAnalysis;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Identifies a form and the published comparison target.</summary>
[Experimental("ENDATIX0007")]
public sealed class DraftDiffRequest : DraftRequest
{
    /// <summary>The comparison target: published.</summary>
    [QueryParam]
    public string? Against { get; set; }
}

/// <summary>Only comparisons with the published version are supported.</summary>
[Experimental("ENDATIX0007")]
public sealed class DraftDiffValidator : Validator<DraftDiffRequest>
{
    /// <inheritdoc/>
    public DraftDiffValidator()
    {
        Include(new DraftRequestValidator());
        RuleFor(x => x.Against).Equal("published");
    }
}

/// <summary>Display the changes from the current live survey to the editor's draft.</summary>
[Experimental("ENDATIX0007")]
public sealed class GetDiff(IMediator mediator) : Endpoint<DraftDiffRequest, Results<Ok<DraftDiffResponse>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/draft/diff");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Compare the draft with the published form";
            s.Responses[200] = "Display changes and the current draft ETag.";
            s.Responses[400] = "Invalid form ID or comparison target.";
            s.Responses[404] = "Form not found.";
            s.Responses[503] = "This form's draft upgrade is pending.";
        });
        Description(b => b.Produces<DraftDiffResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(503));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<DraftDiffResponse>, ProblemHttpResult>> ExecuteAsync(DraftDiffRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ReadDraftDiff(req.FormId), ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }
        HttpContext.Response.Headers.CacheControl = "no-store";
        EntityTagHeader.SetETag(HttpContext.Response, result.Value.Version);
        return TypedResults.Ok(result.Value);
    }
}
