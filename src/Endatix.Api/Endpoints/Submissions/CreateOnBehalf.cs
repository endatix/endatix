using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.Create;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Authenticated endpoint for creating a new form submission on behalf of another user.
/// </summary>
public class CreateOnBehalf(IMediator mediator) : Endpoint<CreateSubmissionOnBehalfRequest, Results<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/onbehalf");
        Permissions(Actions.Submissions.CreateOnBehalf);
        Summary(s =>
        {
            s.Summary = "Create a new submission on behalf of another user";
            s.Description = "Creates a new form submission with the ability to set the submittedBy field.";
            s.Responses[201] = "The submission was successfully created";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot create a submission";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>> ExecuteAsync(CreateSubmissionOnBehalfRequest request, CancellationToken cancellationToken)
    {
        var createCommand = new CreateSubmissionCommand(
            FormId: request.FormId,
            JsonData: request.JsonData!,
            Metadata: request.Metadata,
            CurrentPage: request.CurrentPage,
            IsComplete: request.IsComplete,
            ReCaptchaToken: null, // No reCAPTCHA needed for authenticated requests
            SubmittedBy: request.SubmittedBy,
            RequiredPermission: Actions.Submissions.CreateOnBehalf
        );

        var result = await mediator.Send(createCommand, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<CreateSubmissionOnBehalfResponse>)
            .SetTypedResults<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>();
    }
}
