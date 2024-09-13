using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.Submissions.Create;

namespace Endatix.Api.Endpoints.Submissions;

public class Create(IMediator _mediator) : Endpoint<CreateSubmissionRequest, Results<Created<CreateSubmissionResponse>, BadRequest, NotFound>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a new submission";
            s.Description = "Creates a new form submission";
            s.Responses[200] = "The submission was successfully created";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot create a submission";
        });
    }

    public override async Task<Results<Created<CreateSubmissionResponse>, BadRequest, NotFound>> ExecuteAsync(CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var createCommand = new CreateSubmissionCommand(
            request.FormId,
            request.JsonData!,
            request.Metadata,
            request.CurrentPage,
            request.IsComplete
        );

        var result = await _mediator.Send(createCommand, cancellationToken);

        return result.ToEndpointResponse<
            Results<Created<CreateSubmissionResponse>, BadRequest, NotFound>,
            Submission,
            CreateSubmissionResponse>(SubmissionMapper.Map<CreateSubmissionResponse>);
    }
}
