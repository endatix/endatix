using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
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
public sealed class CreateOnBehalf(
    IMediator mediator,
    ISubmissionPersonalizer personalizer,
    ITenantContext tenant)
    : Endpoint<CreateSubmissionOnBehalfRequest, Results<Created<SubmissionModel>, ProblemHttpResult>>
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
        CreateSubmissionOnBehalfRequest request,
        CancellationToken ct)
    {
        PersonalizationFreeze? freeze = await FreezeAsync(request, ct);
        Result<Submission> result = await mediator.Send(ToCommand(request, freeze), ct);
        await BindAsync(freeze, result, ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.Map<SubmissionModel>)
            .SetTypedResults<Created<SubmissionModel>, ProblemHttpResult>();
    }

    private Task<PersonalizationFreeze?> FreezeAsync(
        CreateSubmissionOnBehalfRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Submitter is null)
        {
            return Task.FromResult<PersonalizationFreeze?>(null);
        }

        return personalizer.FreezeAsync(
            new SubmissionPersonalizationRequest(tenant.TenantId, request.FormId, request.Submitter),
            cancellationToken);
    }

    private static CreateSubmissionCommand ToCommand(
        CreateSubmissionOnBehalfRequest request,
        PersonalizationFreeze? freeze) =>
        new(
            FormId: request.FormId,
            JsonData: request.JsonData,
            Metadata: request.Metadata,
            CurrentPage: request.CurrentPage,
            IsComplete: request.IsComplete,
            ReCaptchaToken: null,
            RequiredPermission: Actions.Submissions.CreateOnBehalf,
            Submitter: request.Submitter,
            PersonalizationSnapshot: freeze?.Snapshot);

    private async Task BindAsync(
        PersonalizationFreeze? freeze,
        Result<Submission> result,
        CancellationToken cancellationToken)
    {
        if (freeze is null || !result.IsSuccess || result.Value.SubmitterId is not long submitterId)
        {
            return;
        }

        await personalizer.BindAsync(freeze, submitterId, cancellationToken);
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
