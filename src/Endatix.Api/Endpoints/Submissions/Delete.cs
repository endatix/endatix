using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.Delete;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for deleting a submission.
/// </summary>
public class Delete(IMediator mediator) : Endpoint<DeleteSubmissionRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Delete("forms/{formId}/submissions/{submissionId}");
        Permissions(Actions.Submissions.Delete, Actions.Submissions.DeleteOwned);
        Summary(s =>
        {
            s.Summary = "Delete a submission";
            s.Description = "Deletes a submission.";
            s.Responses[204] = "Submission deleted successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Submission not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteSubmissionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new DeleteSubmissionCommand(request.FormId, request.SubmissionId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, submission => submission.Id.ToString())
            .SetTypedResults<Ok<string>, BadRequest, NotFound>();
    }
}