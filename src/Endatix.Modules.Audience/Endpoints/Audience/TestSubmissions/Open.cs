using Endatix.Core.Abstractions;
using Endatix.Modules.Audience.Features.TestSubmissions;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.TestSubmissions;

/// <summary>
/// Test as this person: opens a new test submission on the form's current definition with the person's current values.
/// </summary>
public sealed class Open(IMediator mediator, ITenantContext tenantContext)
    : TestSubmissionEndpoint<OpenTestSubmissionRequest>(mediator, tenantContext)
{
    /// <inheritdoc />
    public override void Configure() =>
        ConfigureRoute(new TestRoute(
            "forms/{formId}/audience/test-submissions",
            "Open a test submission as a person",
            "Creates a new test submission for the person on the form's current definition, with their current " +
            "values frozen as personalization (source test). Every call creates a new one. The person's " +
            "personalised link and Submitter are not touched.",
            "Test submission opened. The token opens it on the respondent page.",
            "Invalid input.",
            "Form not found or not enabled, or the person is not on this form."));

    /// <inheritdoc />
    public override async Task<Results<Created<TestSubmissionResponse>, ProblemHttpResult>> ExecuteAsync(
        OpenTestSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await Mediator.Send(
            new OpenTestSubmissionCommand(TenantId, request.FormId, request.MembershipId),
            ct);
        return Respond(result);
    }
}

/// <summary>Validates <see cref="OpenTestSubmissionRequest"/>.</summary>
public sealed class OpenTestSubmissionValidator : Validator<OpenTestSubmissionRequest>
{
    /// <summary>Requires a form and a membership id.</summary>
    public OpenTestSubmissionValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipId).GreaterThan(0);
    }
}

/// <summary>Request to open a test submission as a person.</summary>
public sealed class OpenTestSubmissionRequest
{
    /// <summary>The form to test.</summary>
    public long FormId { get; init; }

    /// <summary>The person on the form to test as.</summary>
    public long MembershipId { get; init; }
}

/// <summary>The new test submission and the token the respondent page opens it with.</summary>
public sealed class TestSubmissionResponse
{
    /// <summary>The new test submission.</summary>
    public long SubmissionId { get; init; }

    /// <summary>The token the respondent page opens the test submission with.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Maps the opened test submission to the wire model.</summary>
    public static TestSubmissionResponse From(OpenedTestSubmissionDto dto) => new()
    {
        SubmissionId = dto.SubmissionId,
        Token = dto.Token,
    };
}
