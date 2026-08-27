using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for partially updating a form submission.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateSubmissionRequest, Results<Ok<PartialUpdateSubmissionResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/{submissionId}");
        Permissions(Actions.Submissions.Edit);
        Summary(s =>
        {
            s.Summary = "Update a form submission";
            s.Description = "Updates a form submission for a given form.";
            s.Responses[200] = "The form submission was updated successfully.";
            s.Responses[400] = "Bad request";
            s.Responses[404] = "Form submission not found";
        });
        Description(builder => builder
            .Produces<PartialUpdateSubmissionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateSubmissionRequest request, CancellationToken ct)
    {
        var updateSubmissionCommand = new PartialUpdateSubmissionCommand(
                    request.SubmissionId,
                    request.FormId,
                    request.IsComplete,
                    request.CurrentPage,
                    request.JsonData,
                    request.Metadata
                );

        var result = await mediator.Send(updateSubmissionCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<PartialUpdateSubmissionResponse>)
            .SetTypedResults<Ok<PartialUpdateSubmissionResponse>, ProblemHttpResult>();
    }
}
