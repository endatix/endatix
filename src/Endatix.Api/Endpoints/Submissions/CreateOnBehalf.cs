using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.UseCases.Submissions.Create;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Authenticated endpoint for creating a new form submission on behalf of another user.
/// </summary>
public sealed class CreateOnBehalf(IMediator mediator)
    : Endpoint<CreateSubmissionOnBehalfRequest, Results<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions/onbehalf");
        Permissions(Actions.Submissions.CreateOnBehalf);
        Summary(s =>
        {
            s.Summary = "Create a new submission on behalf of another user";
            s.Description = "Creates a new form submission with an optional trusted submitter profile.";
            s.Responses[201] = "The submission was successfully created.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot create a submission.";
        });
        Description(builder => builder
            .Produces<CreateSubmissionOnBehalfResponse>(201, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateSubmissionOnBehalfRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateSubmissionCommand(
                FormId: request.FormId,
                JsonData: request.JsonData,
                Metadata: request.Metadata,
                CurrentPage: request.CurrentPage,
                IsComplete: request.IsComplete,
                ReCaptchaToken: null,
                RequiredPermission: Actions.Submissions.CreateOnBehalf,
                Submitter: request.Submitter),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<CreateSubmissionOnBehalfResponse>)
            .SetTypedResults<Created<CreateSubmissionOnBehalfResponse>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request payload for <see cref="CreateOnBehalf"/>.
/// </summary>
public sealed class CreateSubmissionOnBehalfRequest : BaseSubmissionRequest
{
    /// <summary>
    /// Optional trusted submitter profile for API-created submissions.
    /// </summary>
    public SubmitterInput? Submitter { get; set; }
}

/// <summary>
/// Response model for a submission created on behalf of another user.
/// </summary>
public sealed class CreateSubmissionOnBehalfResponse : SubmissionModel;

/// <summary>
/// Validation rules for <see cref="CreateSubmissionOnBehalfRequest"/>.
/// </summary>
public sealed class CreateSubmissionOnBehalfValidator : Validator<CreateSubmissionOnBehalfRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSubmissionOnBehalfValidator"/> class.
    /// </summary>
    public CreateSubmissionOnBehalfValidator()
    {
        this.ApplyBaseSubmissionRules();

        RuleFor(x => x.Submitter!.ExternalSubjectId)
            .NotEmpty()
            .When(x => x.Submitter is not null);

        RuleFor(x => x.Submitter!.DisplayId)
            .NotEmpty()
            .When(x => x.Submitter is not null);
    }
}
