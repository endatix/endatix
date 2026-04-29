using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.Create;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for creating a new form submission.
/// </summary>
public class Create(IMediator mediator, IUserContext userContext) : Endpoint<CreateSubmissionRequest, Results<Created<CreateSubmissionResponse>, Conflict<Microsoft.AspNetCore.Mvc.ProblemDetails>, ProblemHttpResult>>
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
            s.Responses[409] = "A submission already exists for this user and form.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateSubmissionResponse>, Conflict<Microsoft.AspNetCore.Mvc.ProblemDetails>, ProblemHttpResult>> ExecuteAsync(CreateSubmissionRequest request, CancellationToken ct)
    {
        var submittedBy = userContext.GetCurrentUserId();

        var createCommand = new CreateSubmissionCommand(
            FormId: request.FormId,
            JsonData: request.JsonData!,
            Metadata: request.Metadata,
            CurrentPage: request.CurrentPage,
            IsComplete: request.IsComplete,
            ReCaptchaToken: request.ReCaptchaToken,
            SubmittedBy: submittedBy,
            RequiredPermission: Actions.Submissions.Create
        );

        var result = await mediator.Send(createCommand, ct);

        if (result.Status == ResultStatus.Conflict)
        {
            return TypedResults.Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Title = "There was a conflict.",
                Detail = string.Join(Environment.NewLine, result.Errors)
            });
        }

        return result.Status == ResultStatus.Created
            ? TypedResults.Created(string.Empty, SubmissionMapper.Map<CreateSubmissionResponse>(result.Value))
            : result.ToProblem();
    }
}
