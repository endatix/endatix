using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.Create;
using Endatix.Infrastructure.ReCaptcha;
using Errors = Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for creating a new form submission.
/// </summary>
public class Create(IMediator mediator, IGoogleReCaptchaService recaptchaService) : Endpoint<CreateSubmissionRequest, Results<Created<CreateSubmissionResponse>, BadRequest<Errors.ProblemDetails>, NotFound>>
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

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateSubmissionResponse>, BadRequest<Errors.ProblemDetails>, NotFound>> ExecuteAsync(CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        if (recaptchaService.IsEnabled && request.ReCaptchaToken != null)
        {
            var recaptchaResult = await recaptchaService.VerifyTokenAsync(request.ReCaptchaToken, cancellationToken);
            if (!recaptchaResult.IsSuccess)
            {
                return TypedResults.BadRequest(new Errors.ProblemDetails { Title = "Invalid reCAPTCHA token" });
            }
        }

        var createCommand = new CreateSubmissionCommand(
            request.FormId,
            request.JsonData!,
            request.Metadata,
            request.CurrentPage,
            request.IsComplete
        );

        var result = await mediator.Send(createCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<CreateSubmissionResponse>)
            .SetTypedResults<Created<CreateSubmissionResponse>, BadRequest<Errors.ProblemDetails>, NotFound>();
    }
}
