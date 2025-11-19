using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for updating the status of a form submission.
/// </summary>
public class UpdateStatus(IMediator mediator) : Endpoint<UpdateStatusRequest, Results<Ok<UpdateStatusResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/{submissionId}/status");
        Permissions(Actions.Submissions.Edit);
        Summary(s =>
        {
            s.Summary = "Update submission status";
            s.Description = "Updates the status of a form submission.";
            s.Responses[200] = "The submission status was updated successfully.";
            s.Responses[400] = "Invalid status or business rule violation";
            s.Responses[404] = "Submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateStatusResponse>, BadRequest, NotFound>> ExecuteAsync(
        UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateStatusCommand(
            request.SubmissionId,
            request.FormId,
            request.Status
        );

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, result => new UpdateStatusResponse(result.Id, result.Status))
            .SetTypedResults<Ok<UpdateStatusResponse>, BadRequest, NotFound>();
    }
}