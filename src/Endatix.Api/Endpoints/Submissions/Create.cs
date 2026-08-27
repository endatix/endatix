using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.Create;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for creating a new form submission.
/// </summary>
public sealed class Create(IMediator mediator)
    : Endpoint<CreateSubmissionRequest, Results<Created<SubmissionModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Post("forms/{formId}/submissions");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a new submission";
            s.Description = "Creates a new form submission.";
            s.Responses[201] = "The submission was successfully created.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot create a submission.";
            s.Responses[409] = "A submission already exists for this user and form.";
        });
        Description(builder => builder
            .Produces<SubmissionModel>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<SubmissionModel>, ProblemHttpResult>> ExecuteAsync(
        CreateSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateSubmissionCommand(
                FormId: request.FormId,
                JsonData: request.JsonData,
                Metadata: request.Metadata,
                CurrentPage: request.CurrentPage,
                IsComplete: request.IsComplete,
                ReCaptchaToken: request.ReCaptchaToken,
                RequiredPermission: Actions.Submissions.Create,
                SubmitterPrincipal: User),
            ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<SubmissionModel>)
            .SetTypedResults<Created<SubmissionModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request payload for <see cref="Create"/>.
/// </summary>
public sealed class CreateSubmissionRequest : BasePublicSubmissionRequest;

/// <summary>
/// Validation rules for <see cref="CreateSubmissionRequest"/>.
/// </summary>
public sealed class CreateSubmissionValidator : Validator<CreateSubmissionRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSubmissionValidator"/> class.
    /// </summary>
    public CreateSubmissionValidator()
    {
        this.ApplyBaseSubmissionRules();
    }
}
