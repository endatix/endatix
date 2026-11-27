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

/// <summary>Identifies the form whose draft is read or written.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public class DraftRequest
{
    /// <summary>The form ID from the route.</summary>
    public long FormId { get; set; }
}

/// <summary>Validates the draft route.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class DraftRequestValidator : Validator<DraftRequest>
{
    /// <summary>Requires a positive form ID.</summary>
    public DraftRequestValidator() => RuleFor(x => x.FormId).GreaterThan(0);
}

/// <summary>Reads a draft with its current ETag without creating a draft row.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class Get(IMediator mediator) : Endpoint<DraftRequest, Results<Ok<DraftResponse>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/draft");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Read a form draft";
            s.Responses[200] = "The draft and its ETag.";
            s.Responses[400] = "Invalid form ID.";
            s.Responses[404] = "Form not found.";
        });
        Description(b => b.Produces<DraftResponse>().ProducesProblem(400).ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<DraftResponse>, ProblemHttpResult>> ExecuteAsync(DraftRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ReadDraft(req.FormId), ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }
        EntityTagHeader.SetETag(HttpContext.Response, result.Value.Version);
        return TypedResults.Ok(result.Value);
    }
}
