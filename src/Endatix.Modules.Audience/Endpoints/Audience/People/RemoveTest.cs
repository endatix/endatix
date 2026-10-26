using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.People;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Removes test participants from this form, by default with the test submissions opened as them.
/// </summary>
public sealed class RemoveTest(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<RemoveTestPeopleRequest, Results<Ok<RemoveTestPeopleResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/audience/people/test/removal");
        Permissions(Actions.Forms.Edit);
        Summary(Describe);
        Description(builder => builder
            .Produces<RemoveTestPeopleResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(503));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<RemoveTestPeopleResponse>, ProblemHttpResult>> ExecuteAsync(
        RemoveTestPeopleRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new RemoveTestPeopleCommand(
                tenantContext.TenantId,
                request.FormId,
                request.MembershipIds,
                request.AlsoDeleteTestSubmissions,
                request.DryRun),
            ct);

        return TypedResultsBuilder
            .MapResult(result, RemoveTestPeopleResponse.From)
            .SetTypedResults<Ok<RemoveTestPeopleResponse>, ProblemHttpResult>();
    }

    private static void Describe(EndpointSummary summary)
    {
        summary.Summary = "Remove test participants from form";
        summary.Description =
            "Removes the named test participants (every one when membershipIds is null), one at a time, then with " +
            "alsoDeleteTestSubmissions the test submissions opened as them, so their test links answer 404. Real " +
            "participants and their submissions are never touched. dryRun counts and writes nothing. The same " +
            "request again finishes a run that failed part way.";
        summary.Responses[200] = "What was removed, or with dryRun what would be.";
        summary.Responses[400] = "Invalid input, or a real participant in the set; nothing is removed.";
        summary.Responses[403] = "alsoDeleteTestSubmissions without forms.test and submissions.delete.";
        summary.Responses[404] = "Form not found, or a membership never on this form (one removed already counts 0).";
        summary.Responses[503] = "A participant is being changed, or the form's people are being imported; try again.";
    }
}

/// <summary>
/// Validator for removing test participants.
/// </summary>
public sealed class RemoveTestPeopleValidator : Validator<RemoveTestPeopleRequest>
{
    /// <summary>How many participants one request may name; null names every test participant.</summary>
    public const int MAX_MEMBERSHIP_IDS = 500;

    /// <summary>Requires a form, and named participants at most once each, up to the cap.</summary>
    public RemoveTestPeopleValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.MembershipIds!)
            .NotEmpty()
            .Must(ids => ids.Count <= MAX_MEMBERSHIP_IDS)
            .WithMessage($"Name at most {MAX_MEMBERSHIP_IDS} participants, or none for every test participant.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Each participant may be named once.")
            .When(request => request.MembershipIds is not null);
        RuleForEach(request => request.MembershipIds).GreaterThan(0);
    }
}

/// <summary>
/// Request to remove test participants from a form.
/// </summary>
public sealed class RemoveTestPeopleRequest
{
    /// <summary>The form to remove them from.</summary>
    public long FormId { get; init; }

    /// <summary>The test participants to remove; null for every test participant on the form.</summary>
    public IReadOnlyList<long>? MembershipIds { get; init; }

    /// <summary>Also delete the test submissions opened as them.</summary>
    public bool AlsoDeleteTestSubmissions { get; init; }

    /// <summary>Count what would be removed, and write nothing.</summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// What was removed, or with <see cref="DryRun"/> what would be.
/// </summary>
public sealed class RemoveTestPeopleResponse
{
    /// <summary>Test participants removed by this request.</summary>
    public int People { get; init; }

    /// <summary>Test submissions deleted by this request.</summary>
    public int TestSubmissions { get; init; }

    /// <summary>Whether this only counted.</summary>
    public bool DryRun { get; init; }

    /// <summary>Maps the handler's counts to the wire model.</summary>
    public static RemoveTestPeopleResponse From(RemovedTestPeopleDto dto) => new()
    {
        People = dto.People,
        TestSubmissions = dto.TestSubmissions,
        DryRun = dto.DryRun,
    };
}
