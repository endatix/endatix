using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Features.TestSubmissions;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.TestSubmissions;

/// <summary>
/// Start over: deletes a test submission opened as a person and opens a new one for them.
/// </summary>
public sealed class Restart(IMediator mediator, ITenantContext tenantContext)
    : TestSubmissionEndpoint<RestartTestSubmissionRequest>(mediator, tenantContext)
{
    /// <inheritdoc />
    public override void Configure() =>
        ConfigureRoute(new TestRoute(
            "forms/{formId}/audience/test-submissions/{submissionId}/restart",
            "Start over a test submission",
            "Deletes the test submission and opens a new one for the same person, on the form's current " +
            "definition and the person's current values. Only a test submission opened as a person can start over, " +
            "and only with its token.",
            "New test submission opened. The token opens it on the respondent page.",
            "Not a test submission opened as a person.",
            "Form, submission or person not found, or the token is not the submission's."));

    /// <inheritdoc />
    public override async Task<Results<Created<TestSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(
        RestartTestSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(
            new RestartTestSubmissionCommand(TenantId, request.FormId, request.SubmissionId, request.Token),
            ct);
        return Respond(result);
    }
}

/// <summary>Validates <see cref="RestartTestSubmissionRequest"/>.</summary>
public sealed class RestartTestSubmissionValidator : Validator<RestartTestSubmissionRequest>
{
    /// <summary>Requires a form, a submission id and the submission's token.</summary>
    public RestartTestSubmissionValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.SubmissionId).GreaterThan(0);
        RuleFor(request => request.Token).NotEmpty();
    }
}

/// <summary>Request to start over a test submission.</summary>
public sealed class RestartTestSubmissionRequest
{
    /// <summary>The form the test submission belongs to.</summary>
    public long FormId { get; init; }

    /// <summary>The test submission to discard.</summary>
    public long SubmissionId { get; init; }

    /// <summary>The token the test submission was opened with.</summary>
    public string Token { get; init; } = string.Empty;
}
