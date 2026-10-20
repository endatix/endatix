using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Submissions.SetTest;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Marks a submission as test or as real.
/// </summary>
public class SetTest(IMediator mediator) : Endpoint<SetSubmissionTestRequest, Results<Ok<SubmissionModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Patch("forms/{formId}/submissions/{submissionId}/test");
        // submissions.edit too: marking a real submission as test hides it from exports and frees the person's link,
        // which forms.test alone, a permission for testers, must not allow.
        PermissionsAll(Actions.Forms.Test, Actions.Submissions.Edit);
        Summary(s =>
        {
            s.Summary = "Mark a submission as test or real";
            s.Description =
                "Marking as test leaves the submission out of default exports, lifts the one-per-person limit and " +
                "frees the personalised link that opened it. Marking as real restores the limit. Requires forms.test and " +
                "submissions.edit.";
            s.ExampleRequest = new SetSubmissionTestRequest { FormId = 1, SubmissionId = 2, IsTest = true };
            s.Responses[200] = "The submission was marked.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[403] = "The caller lacks forms.test or submissions.edit.";
            s.Responses[404] = "Form or submission not found.";
            s.Responses[409] =
                "The person already has a real submission on this form, by submitter or through the same personalised link " +
                "(errorCode submission_exists, submissionId).";
        });
        Description(builder => builder
            .Produces<SubmissionModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<SubmissionModel>, ProblemHttpResult>> ExecuteAsync(
        SetSubmissionTestRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new SetSubmissionTestCommand(request.FormId, request.SubmissionId, request.IsTest!.Value),
            ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionMapper.MapFromDto)
            .SetTypedResults<Ok<SubmissionModel>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request to mark a submission as test or real.
/// </summary>
public sealed class SetSubmissionTestRequest
{
    /// <summary>The form the submission belongs to.</summary>
    public long FormId { get; init; }

    /// <summary>The submission to mark.</summary>
    public long SubmissionId { get; init; }

    /// <summary><see langword="true"/> marks the submission as test, <see langword="false"/> as real.</summary>
    public bool? IsTest { get; init; }
}

/// <summary>
/// Validates <see cref="SetSubmissionTestRequest"/>.
/// </summary>
public sealed class SetSubmissionTestValidator : Validator<SetSubmissionTestRequest>
{
    /// <summary>Requires the form and submission ids and the flag.</summary>
    public SetSubmissionTestValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
        RuleFor(x => x.SubmissionId).GreaterThan(0);
        RuleFor(x => x.IsTest).NotNull();
    }
}
