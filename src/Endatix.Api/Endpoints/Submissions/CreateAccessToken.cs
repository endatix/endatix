using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.CreateAccessToken;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for generating submission access tokens.
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
            s.Summary = "Generate access token for submission";
            s.Description =
                "Creates a signed token for sharing submission access with granular permissions. " +
                $"Expiry is 1–{CreateAccessTokenValidator.MaxExpiryMinutes} minutes (up to 60 days).";
            s.ExampleRequest = new CreateAccessTokenRequest
            {
                FormId = 1,
                SubmissionId = 42,
                ExpiryMinutes = 1440,
                Permissions = ["view", "edit"]
            };
            s.ResponseExamples[200] = new CreateAccessTokenResponse(
                "42.1769113804.rw.qRHaddrBDolnRRMq",
                DateTime.UtcNow.AddDays(1),
                ["view", "edit"]);
            s.Responses[200] = "Access token generated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Submission not found.";
        });
        Description(builder => builder
            .Produces<CreateAccessTokenResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<CreateAccessTokenResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateAccessTokenRequest request,
        CancellationToken ct)
    {
        var command = new CreateAccessTokenCommand(
            request.FormId,
            request.SubmissionId,
            request.ExpiryMinutes!.Value,
            request.Permissions!
        );
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, dto => new CreateAccessTokenResponse(dto.Token, dto.ExpiresAt, dto.Permissions))
            .SetTypedResults<Ok<CreateAccessTokenResponse>, ProblemHttpResult>();
    }
}
