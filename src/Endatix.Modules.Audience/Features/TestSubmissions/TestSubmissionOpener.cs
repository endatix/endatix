using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>A person on a form, as a test submission is opened for them.</summary>
internal sealed record TestSubject(
    long TenantId,
    long FormId,
    long ActiveDefinitionId,
    long MembershipId,
    long MemberId,
    string Identifier);

/// <summary>
/// That person, and the form already loaded for them. The form supplies the token expiry; nothing else is read
/// from it.
/// </summary>
internal sealed record TestOpening(TestSubject Subject, Form Form);

/// <summary>A test submission just opened, and the token the respondent page works on it with.</summary>
public sealed record OpenedTestSubmissionDto(long SubmissionId, string Token);

/// <summary>
/// The one way to build a test submission: the form's active definition and the person's current values, flagged
/// as test. It never reads or writes Links, <c>Member.SubmitterId</c> or Submitters, so a person's personalised link
/// and one-response gate stay as they were.
/// </summary>
internal sealed class TestSubmissionOpener(
    IAudienceDbContext db,
    IRepository<Submission> submissions,
    ISubmissionTokenService tokens)
{
    /// <summary>
    /// Mints the token from the form already loaded, attaches it, then saves. A Start over's delete is already
    /// tracked, so it commits in that same save. A token that cannot be minted saves nothing, and the old test
    /// submission stays.
    /// </summary>
    public async Task<Result<OpenedTestSubmissionDto>> OpenAsync(
        TestOpening opening,
        CancellationToken cancellationToken)
    {
        var cells = await SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(opening.Subject.TenantId, opening.Subject.MembershipId),
            cancellationToken);
        return await OpenAsync(opening, cells, cancellationToken);
    }

    /// <summary>The insert. The token is on the submission before this saves, including a Start over's delete.</summary>
    internal async Task<Result<OpenedTestSubmissionDto>> OpenAsync(
        TestOpening opening,
        IReadOnlyCollection<SnapshotCell> cells,
        CancellationToken cancellationToken)
    {
        var submission = Build(opening.Subject, cells, DateTime.UtcNow);
        var token = await tokens.NewTokenAsync(opening.Form, cancellationToken);
        submission.UpdateToken(token);
        await submissions.AddAsync(submission, cancellationToken);
        return Result<OpenedTestSubmissionDto>.Created(new OpenedTestSubmissionDto(submission.Id, token.Value));
    }

    /// <summary>
    /// No Submitter and no single-response gate: a test submission is outside one-per-person. The person's
    /// identifier is its label only.
    /// </summary>
    internal static Submission Build(TestSubject subject, IReadOnlyCollection<SnapshotCell> cells, DateTime capturedAt)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            subject.TenantId,
            subject.FormId,
            subject.ActiveDefinitionId,
            "{}",
            IsComplete: false,
            SubmitterId: null,
            SubmitterDisplayId: subject.Identifier,
            IsTestSubmission: true,
            EnforceSingleSubmissionGate: false,
            StartSubmission: true));
        submission.FreezeOnBehalf(PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, subject.Identifier, capturedAt, PersonalizationSources.Test, subject.MemberId),
            cells));
        return submission;
    }
}
