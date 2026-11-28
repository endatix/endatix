using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.GetByToken;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Gets a submission and its bound definition using the respondent's token.
/// </summary>
public class GetByToken(IMediator mediator) : Endpoint<GetByTokenRequest, Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/by-token/{submissionToken}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get a single submission by token";
            s.Description = "Gets a submission and its bound form definition by submission token and form ID.";
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
    public override async Task<Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>> ExecuteAsync(GetByTokenRequest request, CancellationToken ct)
    {
        var query = new GetByTokenQuery(request.FormId, request.SubmissionToken!);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.MapToSubmissionDetails)
            .SetTypedResults<Ok<SubmissionDetailsModel>, ProblemHttpResult>();
    }
}
