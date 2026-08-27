using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.GetById;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for getting a form submission by ID.
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetByIdRequest, Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/{submissionId}");
        Permissions(Actions.Submissions.View);
        Summary(s =>
        {
            s.Summary = "Get a single submission";
            s.Description = "Gets a single submission based of its Id and its respective formId";
            s.Responses[200] = "The Submission was retrieved successfully";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form submission not found";
        });
        Description(builder => builder
            .Produces<SubmissionDetailsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>> ExecuteAsync(GetByIdRequest request, CancellationToken ct)
    {
        var getSubmissionByIdQuery = new GetByIdQuery(request.FormId, request.SubmissionId);
        var result = await mediator.Send(getSubmissionByIdQuery, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.MapToSubmissionDetails)
            .SetTypedResults<Ok<SubmissionDetailsModel>, ProblemHttpResult>();
    }
}
