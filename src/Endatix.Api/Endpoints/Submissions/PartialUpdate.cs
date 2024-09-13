using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for partially updating a form submission.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateSubmissionRequest, Results<Ok<PartialUpdateSubmissionResponse>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/{submissionId}");
        AllowAnonymous();
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
    /// Executes the HTTP request for partially updating a form submission.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task<Results<Ok<PartialUpdateSubmissionResponse>, BadRequest, NotFound>> ExecuteAsync(PartialUpdateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var updateSubmissionCommand = new PartialUpdateSubmissionCommand(
                    request.SubmissionId,
                    request.FormId,
                    request.IsComplete,
                    request.CurrentPage,
                    request.JsonData,
                    request.Metadata
                );

        var result = await mediator.Send(updateSubmissionCommand, cancellationToken);

        return result.ToEndpointResponse<
            Results<Ok<PartialUpdateSubmissionResponse>, BadRequest, NotFound>,
            Submission,
            PartialUpdateSubmissionResponse>(SubmissionMapper.Map<PartialUpdateSubmissionResponse>);
    }
}
