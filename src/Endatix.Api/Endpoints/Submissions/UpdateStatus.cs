using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.UpdateStatus;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

public class UpdateStatus(IMediator mediator) : Endpoint<UpdateSubmissionStatusRequest, Results<Ok<UpdateSubmissionStatusResponse>, BadRequest, NotFound>>
{
    public override void Configure()
    {
        Post("forms/{formId}/submissions/{submissionId}/status");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Update submission status";
            s.Description = "Updates the status of a form submission.";
            s.Responses[200] = "The submission status was updated successfully.";
            s.Responses[400] = "Invalid status or business rule violation";
            s.Responses[404] = "Submission not found";
        });
    }

    public override async Task<Results<Ok<UpdateSubmissionStatusResponse>, BadRequest, NotFound>> ExecuteAsync(
        UpdateSubmissionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSubmissionStatusCommand(
            request.SubmissionId,
            request.FormId,
            request.Status
        );

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .FromResult(result)
            .SetTypedResults<Ok<UpdateSubmissionStatusResponse>, BadRequest, NotFound>();
    }
}

public record UpdateSubmissionStatusRequest
{
    public long SubmissionId { get; init; }
    public long FormId { get; init; }
    public string Status { get; init; } = string.Empty;
} 