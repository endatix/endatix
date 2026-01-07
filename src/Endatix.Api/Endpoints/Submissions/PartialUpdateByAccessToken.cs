using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for partially updating a form submission by access token.
/// </summary>
public class PartialUpdateByAccessToken(IMediator mediator)
    : Endpoint<PartialUpdateByAccessTokenRequest, Results<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/by-access-token/{token}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Update a form submission by access token";
            s.Description = "Updates a form submission using a short-lived access token";
            s.Responses[200] = "The form submission was updated successfully";
            s.Responses[400] = "Bad request or invalid token/permissions";
            s.Responses[404] = "Submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>>
        ExecuteAsync(PartialUpdateByAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new PartialUpdateByAccessTokenCommand(
            request.Token!,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData,
            request.Metadata
        );

        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<PartialUpdateSubmissionByTokenResponse>)
            .SetTypedResults<Ok<PartialUpdateSubmissionByTokenResponse>, ProblemHttpResult>();
    }
}
