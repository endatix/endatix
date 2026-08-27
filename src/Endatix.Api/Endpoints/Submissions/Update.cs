using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for updating a form submission.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateSubmissionRequest, Results<Ok<UpdateSubmissionResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/submissions/{submissionId}");
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
            .Produces<UpdateSubmissionResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(UpdateSubmissionRequest request, CancellationToken ct)
    {
        var updateSubmissionCommand = new UpdateSubmissionCommand(
            request.SubmissionId,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData!,
            request.Metadata
        );

        var result = await mediator.Send(updateSubmissionCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<UpdateSubmissionResponse>)
            .SetTypedResults<Ok<UpdateSubmissionResponse>, ProblemHttpResult>();
    }
}
