using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Infrastructure.ReCaptcha;
using Endatix.Core.UseCases.Submissions.PartialUpdateByToken;
using Microsoft.AspNetCore.Http;
using Errors = Microsoft.AspNetCore.Mvc;
using FastEndpoints;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for partially updating a form submission by token.
/// </summary>
public class PartialUpdateByToken(IMediator mediator, IGoogleReCaptchaService recaptchaService) : Endpoint<PartialUpdateSubmissionByTokenRequest, Results<Ok<PartialUpdateSubmissionByTokenResponse>, BadRequest<Errors.ProblemDetails>, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/by-token/{submissionToken}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Update a form submission by token";
            s.Description = "Updates a form submission for a given form by token.";
            s.Responses[200] = "The form submission was updated successfully.";
            s.Responses[400] = "Bad request";
            s.Responses[404] = "Form submission not found or invalid token";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateSubmissionByTokenResponse>, BadRequest<Errors.ProblemDetails>, NotFound>> ExecuteAsync(PartialUpdateSubmissionByTokenRequest request, CancellationToken cancellationToken)
    {
        var recaptchaResult = await request.ValidateReCaptchaAsync(recaptchaService, cancellationToken);
        if (!recaptchaResult.IsSuccess)
        {
            return TypedResults.BadRequest(new Errors.ProblemDetails
            {
                Title = "Invalid reCAPTCHA token",
                Detail = recaptchaResult.ErrorCodes.FirstOrDefault()
            });
        }

        var updateSubmissionCommand = new PartialUpdateSubmissionByTokenCommand(
            request.SubmissionToken,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData,
            request.Metadata
        );

        var result = await mediator.Send(updateSubmissionCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<PartialUpdateSubmissionByTokenResponse>)
            .SetTypedResults<Ok<PartialUpdateSubmissionByTokenResponse>, BadRequest<Errors.ProblemDetails>, NotFound>();
    }
}