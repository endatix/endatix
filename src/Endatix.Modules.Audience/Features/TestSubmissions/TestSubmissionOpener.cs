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
    /// Mints the token from the form already loaded, attaches it, then saves. A token that cannot be minted saves
    /// nothing.
    /// </summary>
    public async Task<Result<OpenedTestSubmissionDto>> OpenAsync(
        TestOpening opening,
        CancellationToken cancellationToken) =>
        await AddAsync(await PrepareAsync(opening, cancellationToken), cancellationToken);

    /// <summary>
    /// Every read an open makes: the person's values and the token. A Start over runs it before its transaction, so
    /// the transaction only writes and never waits for a second connection.
    /// </summary>
    public async Task<Submission> PrepareAsync(TestOpening opening, CancellationToken cancellationToken)
    {
        var cells = await SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(opening.Subject.TenantId, opening.Subject.MembershipId),
            cancellationToken);
        return await PrepareAsync(opening, cells, cancellationToken);
    }

    /// <summary>The token is on the submission before anything saves it.</summary>
    internal async Task<Submission> PrepareAsync(
        TestOpening opening,
        IReadOnlyCollection<SnapshotCell> cells,
        CancellationToken cancellationToken)
    {
        var submission = Build(opening.Subject, cells, DateTime.UtcNow);
        submission.UpdateToken(await tokens.NewTokenAsync(opening.Form, cancellationToken));
        return submission;
    }

    /// <summary>The insert. A Start over's delete is already tracked, so it commits in this same save.</summary>
    public async Task<Result<OpenedTestSubmissionDto>> AddAsync(Submission prepared, CancellationToken cancellationToken)
    {
        await submissions.AddAsync(prepared, cancellationToken);
        return Result<OpenedTestSubmissionDto>.Created(new OpenedTestSubmissionDto(prepared.Id, prepared.Token!.Value));
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
        submission.FreezeForTest(PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, subject.Identifier, capturedAt, PersonalizationSources.Test, subject.MemberId),
            cells));
        return submission;
    }
}
