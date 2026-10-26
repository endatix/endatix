using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Test links: opens <see cref="RunsEach"/> new test submissions for each Test participant, and a fill-in token for
/// each. <see cref="MembershipIds"/> <see langword="null"/> means every Test participant on the form.
/// </summary>
public sealed record CreateTestLinksCommand(
    long TenantId,
    long FormId,
    IReadOnlyCollection<long>? MembershipIds,
    int RunsEach,
    int ExpiryMinutes) : ICommand<Result<TestLinksDto>>;

/// <summary>One run's test submission and the token that opens it.</summary>
public sealed record TestLinkDto(
    long MembershipId,
    string Identifier,
    int Run,
    long SubmissionId,
    string Token,
    DateTime ExpiresAt);

/// <summary>The links of one call, by membership, then run.</summary>
public sealed record TestLinksDto(IReadOnlyList<TestLinkDto> Items);

/// <summary>What one call may ask for.</summary>
internal static class TestLinkLimits
{
    public const int MAX_RUNS = 10;

    public const int MAX_SUBMISSIONS = 500;

    /// <summary>An hour: a shorter link would lapse before a reviewer opens it.</summary>
    public const int MIN_EXPIRY_MINUTES = 60;

    public const int MAX_EXPIRY_MINUTES = ISubmissionAccessTokenService.MAX_EXPIRY_MINUTES;
}

/// <summary>
/// Opens every submission in one save under the form's lock and mints the tokens before it commits, so a call either
/// opens all its submissions or none. The tokens are unstamped and fill-in only, as Copy test link's are.
/// </summary>
internal sealed class CreateTestLinksHandler(
    TestSubjects subjects,
    TestSubmissionOpener opener,
    ISubmissionAccessTokenService accessTokens)
    : ICommandHandler<CreateTestLinksCommand, Result<TestLinksDto>>
{
    private static readonly string[] _fillIn = [SubmissionAccessTokenPermissions.Submit.Name];

    public async Task<Result<TestLinksDto>> Handle(CreateTestLinksCommand request, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(request, cancellationToken);
        return plan.IsSuccess
            ? await opener.OpenManyAsync(
                plan.Value.Batch,
                opened => Mint(plan.Value.Runs, opened, request.ExpiryMinutes),
                cancellationToken)
            : plan.ToErrorResult<TestLinksDto>();
    }

    /// <summary>Everything is checked here, before anything is written.</summary>
    private async Task<Result<TestLinkPlan>> PlanAsync(CreateTestLinksCommand request, CancellationToken cancellationToken)
    {
        var tenant = TenantFormGate.EnsureTenant(request.TenantId);
        if (!tenant.IsSuccess)
        {
            return tenant.ToErrorResult<TestLinkPlan>();
        }

        var key = new TestSubjectKey(request.TenantId, request.FormId);
        var candidates = await subjects.CandidatesAsync(key, request.MembershipIds?.Distinct().ToList(), cancellationToken);
        if (!candidates.IsSuccess)
        {
            return candidates.ToErrorResult<TestLinkPlan>();
        }

        var runs = RunsOf(candidates.Value.People, request.RunsEach);
        return runs.IsSuccess
            ? new TestLinkPlan(new TestBatch(candidates.Value.Form, [.. runs.Value.Select(run => run.Subject)]), runs.Value)
            : runs.ToErrorResult<TestLinkPlan>();
    }

    /// <summary>
    /// One run per Test participant and run number. A real participant is refused here: the opener opens a test
    /// submission for anyone on the form.
    /// </summary>
    private static Result<List<TestRun>> RunsOf(IReadOnlyList<TestCandidate> people, int runsEach)
    {
        if (people.Any(person => !person.IsTest))
        {
            return Result.Invalid(new ValidationError("Test links are only for Test participants."));
        }

        var submissions = (long)people.Count * runsEach;
        return submissions <= TestLinkLimits.MAX_SUBMISSIONS
            ? Result.Success<List<TestRun>>([
                .. people.SelectMany(person =>
                    Enumerable.Range(1, runsEach).Select(number => new TestRun(person.Subject, number))),
            ])
            : Result.Invalid(new ValidationError(
                $"{submissions} test submissions were asked for; the limit is {TestLinkLimits.MAX_SUBMISSIONS} per call."));
    }

    /// <summary>Runs inside the save's transaction: a token that cannot be minted rolls every submission back.</summary>
    private Result<TestLinksDto> Mint(
        IReadOnlyList<TestRun> runs,
        IReadOnlyList<OpenedTestSubmissionDto> opened,
        int expiryMinutes)
    {
        var items = new List<TestLinkDto>(opened.Count);
        foreach (var (run, submission) in runs.Zip(opened))
        {
            var token = accessTokens.GenerateAccessToken(submission.SubmissionId, expiryMinutes, _fillIn);
            if (!token.IsSuccess)
            {
                return token.ToErrorResult<TestLinksDto>();
            }

            items.Add(LinkOf(run, submission.SubmissionId, token.Value));
        }

        return Result.Success(new TestLinksDto(items));
    }

    private static TestLinkDto LinkOf(TestRun run, long submissionId, SubmissionAccessTokenDto token) =>
        new(run.Subject.MembershipId, run.Subject.Identifier, run.Number, submissionId, token.Token, token.ExpiresAt);

    private sealed record TestRun(TestSubject Subject, int Number);

    private sealed record TestLinkPlan(TestBatch Batch, IReadOnlyList<TestRun> Runs);
}
