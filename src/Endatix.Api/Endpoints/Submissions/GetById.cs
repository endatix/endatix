using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for getting a form submission by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetByIdRequest, Results<Ok<SubmissionModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/{submissionId}");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "Get a single submission";
            s.Description = "Gets a single submission based of its Id and its respective formId";
            s.Responses[200] = "The Submission was retrieved successfully";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<SubmissionModel>, BadRequest, NotFound>> ExecuteAsync(GetByIdRequest request, CancellationToken cancellationToken)
    {
        var getSubmissionByIdQuery = new GetByIdQuery(request.FormId, request.SubmissionId);
        var result = await mediator.Send(getSubmissionByIdQuery, cancellationToken);

        return TypedResultsBuilder
                    .MapResult(result, SubmissionMapper.Map<SubmissionModel>)
                    .SetTypedResults<Ok<SubmissionModel>, BadRequest, NotFound>();

    }
}
