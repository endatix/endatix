using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Api.Submissions;

/// <summary>
/// Endpoint for updating a form submission.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateSubmissionRequest, Results<Ok<UpdateSubmissionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}/submissions/{submissionId}");
        Roles("Admin");
        Summary(s =>
        {
            s.Summary = "Update a form submission";
            s.Description = "Updates a form submission for a given form.";
            s.Responses[200] = "The form submission was updated successfully.";
            s.Responses[400] = "Bad request";
            s.Responses[404] = "Form submission not found";
        });
    }

    /// <summary>
    /// Executes the HTTP request for updating a form definition.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task<Results<Ok<UpdateSubmissionResponse>, BadRequest, NotFound>> ExecuteAsync(UpdateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var updateSubmissionCommand = new UpdateSubmissionCommand(
            request.SubmissionId,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData!,
            request.Metadata
        );

        var result = await mediator.Send(updateSubmissionCommand, cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<UpdateSubmissionResponse>, BadRequest, NotFound>,
            Submission,
            UpdateSubmissionResponse>(SubmissionMapper.Map<UpdateSubmissionResponse>);
    }
}
