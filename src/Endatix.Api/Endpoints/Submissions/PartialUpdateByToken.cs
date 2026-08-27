using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using FastEndpoints;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for partially updating a form submission by token.
/// </summary>
public class PartialUpdateByToken(IMediator mediator) : Endpoint<PartialUpdateSubmissionByTokenRequest, Results<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/by-token/{submissionToken}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Update a form submission by token";
            s.Description = "Updates a form submission for a given form by token.";
            s.Responses[200] = "The form submission was updated successfully.";
            s.Responses[400] = "Bad request";
            s.Responses[404] = "Form submission not found or invalid token";
        });
        Description(builder => builder
            .Produces<PartialUpdateSubmissionByTokenResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateSubmissionByTokenRequest request, CancellationToken ct)
    {
        var updateSubmissionCommand = new PartialUpdateSubmissionByTokenCommand(
            request.SubmissionToken,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData,
            request.Metadata,
            request.ReCaptchaToken
        );

        var result = await mediator.Send(updateSubmissionCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<PartialUpdateSubmissionByTokenResponse>)
            .SetTypedResults<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>();
    }
}
