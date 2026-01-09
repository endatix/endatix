using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.GetByAccessToken;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for getting a submission using an access token.
/// </summary>
public class GetByAccessToken(IMediator mediator)
    : Endpoint<GetByAccessTokenRequest, Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/by-access-token/{token}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get submission using access token";
            s.Description = "Retrieves submission data using a short-lived access token";
            s.Responses[200] = "Submission retrieved successfully";
            s.Responses[400] = "Invalid token or permissions";
            s.Responses[404] = "Submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<SubmissionDetailsModel>, ProblemHttpResult>> ExecuteAsync(
        GetByAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetByAccessTokenQuery(request.FormId, request.Token!);
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.MapToSubmissionDetails)
            .SetTypedResults<Ok<SubmissionDetailsModel>, ProblemHttpResult>();
    }
}
