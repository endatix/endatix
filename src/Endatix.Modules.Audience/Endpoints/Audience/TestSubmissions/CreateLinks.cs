using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Audience.Features.TestSubmissions;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.TestSubmissions;

/// <summary>
/// Test links: one or more one-run links per Test participant, for reviewers without a login, opened all or nothing.
/// It needs what opening a test submission needs, <c>forms.test</c> and <c>forms.edit</c>, and no submissions
/// permission: it opens its own test submissions rather than sharing existing ones.
/// </summary>
public sealed class CreateLinks(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<CreateTestLinksRequest, Results<Ok<TestLinksResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/audience/test-links");
        PermissionsAll(Actions.Forms.Test, Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Create test links for Test participants";
            summary.Description =
                "Opens runsEach new test submissions for each named Test participant, or for every Test participant " +
                "when membershipIds is null, and returns a fill-in access token for each. Either every submission " +
                "is opened or none is.";
            summary.Responses[200] = "The links, by membership, then run.";
            summary.Responses[400] =
                "Invalid input, a real participant in the set, or more than 500 test submissions asked for.";
            summary.Responses[404] = "Form not found or not enabled, or a person is not on this form.";
            summary.Responses[503] = "The form is being deleted, or the save lost to a concurrent write; try again shortly.";
        });
        Description(builder => builder
            .Produces<TestLinksResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(503));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<TestLinksResponse>, ProblemHttpResult>> ExecuteAsync(
        CreateTestLinksRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateTestLinksCommand(
                tenantContext.TenantId,
                request.FormId,
                request.MembershipIds,
                request.RunsEach,
                request.ExpiryMinutes),
            ct);
        return TypedResultsBuilder
            .MapResult(result, TestLinksResponse.From)
            .SetTypedResults<Ok<TestLinksResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates <see cref="CreateTestLinksRequest"/>.</summary>
public sealed class CreateTestLinksValidator : Validator<CreateTestLinksRequest>
{
    /// <summary>Runs 1 to 10, an expiry of an hour to 60 days, and named memberships, if any, that could fit the cap.</summary>
    public CreateTestLinksValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.RunsEach).InclusiveBetween(1, TestLinkLimits.MAX_RUNS);
        RuleFor(request => request.ExpiryMinutes)
            .InclusiveBetween(TestLinkLimits.MIN_EXPIRY_MINUTES, TestLinkLimits.MAX_EXPIRY_MINUTES);
        RuleFor(request => request.MembershipIds)
            .NotEmpty()
            .WithMessage("Name at least one participant, or send null for every Test participant.")
            .Must(ids => ids!.Count <= TestLinkLimits.MAX_SUBMISSIONS)
            .WithMessage($"The limit is {TestLinkLimits.MAX_SUBMISSIONS} test submissions per call.")
            .When(request => request.MembershipIds is not null);
        RuleForEach(request => request.MembershipIds).GreaterThan(0);
    }
}

/// <summary>Request to create test links.</summary>
public sealed class CreateTestLinksRequest
{
    /// <summary>The form to test.</summary>
    public long FormId { get; init; }

    /// <summary>The Test participants to create links for; <see langword="null"/> for every Test participant.</summary>
    public List<long>? MembershipIds { get; init; }

    /// <summary>How many links, each its own test submission, each participant gets: 1 to 10.</summary>
    public int RunsEach { get; init; }

    /// <summary>How long each link works, in minutes: 60 to 86,400 (60 days).</summary>
    public int ExpiryMinutes { get; init; }
}

/// <summary>The links one call created.</summary>
public sealed class TestLinksResponse
{
    /// <summary>The links, by membership, then run.</summary>
    public IReadOnlyList<TestLinkResponse> Items { get; init; } = [];

    /// <summary>Maps the created links to the wire model.</summary>
    public static TestLinksResponse From(TestLinksDto dto) => new()
    {
        Items = [.. dto.Items.Select(TestLinkResponse.From)],
    };
}

/// <summary>One run's test submission and the access token that opens it on the respondent page.</summary>
public sealed class TestLinkResponse
{
    /// <summary>The Test participant's membership on the form.</summary>
    public long MembershipId { get; init; }

    /// <summary>The participant's identifier.</summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>The run, from 1.</summary>
    public int Run { get; init; }

    /// <summary>The test submission opened for this run.</summary>
    public long SubmissionId { get; init; }

    /// <summary>A fill-in (<c>submit</c>) access token for the submission.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>When the token stops working, in UTC.</summary>
    public DateTime ExpiresAt { get; init; }

    internal static TestLinkResponse From(TestLinkDto dto) => new()
    {
        MembershipId = dto.MembershipId,
        Identifier = dto.Identifier,
        Run = dto.Run,
        SubmissionId = dto.SubmissionId,
        Token = dto.Token,
        ExpiresAt = dto.ExpiresAt,
    };
}
