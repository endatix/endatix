using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

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
    IAudienceDbContext db,
    TestSubjects subjects,
    TestSubmissionOpener opener,
    ISubmissionAccessTokenService accessTokens)
    : ICommandHandler<CreateTestLinksCommand, Result<TestLinksDto>>
{
    private static readonly string[] _fillIn = [SubmissionAccessTokenPermissions.Submit.Name];

    public async Task<Result<TestLinksDto>> Handle(CreateTestLinksCommand request, CancellationToken cancellationToken)
    {
        var tenant = TenantFormGate.EnsureTenant(request.TenantId);
        if (!tenant.IsSuccess)
        {
            return tenant.ToErrorResult<TestLinksDto>();
        }

        var chosen = await TestParticipantsAsync(request, cancellationToken);
        if (!chosen.IsSuccess)
        {
            return chosen.ToErrorResult<TestLinksDto>();
        }

        var key = new TestSubjectKey(request.TenantId, request.FormId);
        var openings = await subjects.ManyAsync(key, chosen.Value, cancellationToken);
        if (!openings.IsSuccess)
        {
            return openings.ToErrorResult<TestLinksDto>();
        }

        var runs = Runs(openings.Value, request.RunsEach);
        return await opener.OpenManyAsync(
            [.. runs.Select(run => run.Opening)],
            opened => Mint(runs, opened, request.ExpiryMinutes),
            cancellationToken);
    }

    /// <summary>
    /// The memberships to open for: each named one on the form and a Test participant, or every Test participant.
    /// A real participant is refused here: the opener opens a test submission for anyone on the form.
    /// </summary>
    private async Task<Result<IReadOnlyCollection<long>>> TestParticipantsAsync(
        CreateTestLinksCommand request,
        CancellationToken cancellationToken)
    {
        var onForm = FormParticipants.OnForm(db, request.TenantId, request.FormId);
        if (request.MembershipIds is null)
        {
            var everyTest = await onForm.Where(membership => membership.IsTest)
                .Select(membership => membership.Id)
                .ToListAsync(cancellationToken);
            return WithinCap(everyTest, request.RunsEach);
        }

        var named = request.MembershipIds.Distinct().ToList();
        var found = await onForm.Where(membership => named.Contains(membership.Id))
            .Select(membership => new { membership.Id, membership.IsTest })
            .ToListAsync(cancellationToken);
        if (found.Count < named.Count)
        {
            return Result.NotFound(TestSubjects.PERSON_NOT_FOUND);
        }

        return found.All(membership => membership.IsTest)
            ? WithinCap(named, request.RunsEach)
            : Result.Invalid(new ValidationError("Test links are only for Test participants."));
    }

    private static Result<IReadOnlyCollection<long>> WithinCap(List<long> membershipIds, int runsEach)
    {
        var submissions = (long)membershipIds.Count * runsEach;
        return submissions <= TestLinkLimits.MAX_SUBMISSIONS
            ? Result.Success<IReadOnlyCollection<long>>(membershipIds)
            : Result.Invalid(new ValidationError(
                $"{submissions} test submissions were asked for; the limit is {TestLinkLimits.MAX_SUBMISSIONS} per call."));
    }

    private static List<TestRun> Runs(IReadOnlyList<TestOpening> openings, int runsEach) =>
    [
        .. openings.SelectMany(opening =>
            Enumerable.Range(1, runsEach).Select(number => new TestRun(opening, number))),
    ];

    /// <summary>Runs inside the save's transaction: a token that cannot be minted rolls every submission back.</summary>
    private Result<TestLinksDto> Mint(
        IReadOnlyList<TestRun> runs,
        IReadOnlyList<OpenedTestSubmissionDto> opened,
        int expiryMinutes)
    {
        var items = new List<TestLinkDto>(opened.Count);
        for (var index = 0; index < opened.Count; index++)
        {
            var submissionId = opened[index].SubmissionId;
            var token = accessTokens.GenerateAccessToken(submissionId, expiryMinutes, _fillIn);
            if (!token.IsSuccess)
            {
                return token.ToErrorResult<TestLinksDto>();
            }

            var subject = runs[index].Opening.Subject;
            items.Add(new TestLinkDto(
                subject.MembershipId,
                subject.Identifier,
                runs[index].Number,
                submissionId,
                token.Value.Token,
                token.Value.ExpiresAt));
        }

        return Result.Success(new TestLinksDto(items));
    }

    private sealed record TestRun(TestOpening Opening, int Number);
}
