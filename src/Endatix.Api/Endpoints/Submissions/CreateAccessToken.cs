using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.CreateAccessToken;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for generating short-lived submission access tokens.
/// </summary>
public class CreateAccessToken(IMediator mediator)
    : Endpoint<CreateAccessTokenRequest, Results<Ok<CreateAccessTokenResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/{submissionId}/access-token");
        Permissions(Actions.Submissions.View, Actions.Submissions.Edit, Actions.Submissions.Export);
        Summary(s =>
        {
            s.Summary = "Generate short-lived access token for submission";
            s.Description = "Creates a temporary signed token for sharing submission access with granular permissions";
            s.Responses[200] = "Access token generated successfully";
            s.Responses[400] = "Invalid input data";
            s.Responses[404] = "Submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<CreateAccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAccessTokenCommand(
            request.FormId,
            request.SubmissionId,
            request.ExpiryMinutes!.Value,
            request.Permissions!
        );
        var result = await mediator.Send(command, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, dto => new CreateAccessTokenResponse(dto.Token, dto.ExpiresAt, dto.Permissions))
            .SetTypedResults<Ok<CreateAccessTokenResponse>, ProblemHttpResult>();
    }
}
