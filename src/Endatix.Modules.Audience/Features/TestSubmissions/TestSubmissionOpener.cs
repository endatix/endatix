using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>A person on a form, as a test submission is opened for them.</summary>
internal sealed record TestSubject(
    long TenantId,
    long FormId,
    long MembershipId,
    long MemberId,
    string Identifier);

/// <summary>
/// That person, and the form already loaded for them. The form supplies the token expiry; nothing else is read
/// from it.
/// </summary>
internal sealed record TestOpening(TestSubject Subject, Form Form);

/// <summary>
/// The people, once per run, to open test submissions for on one form, already loaded. The form supplies the token
/// expiry.
/// </summary>
internal sealed record TestBatch(Form Form, IReadOnlyList<TestSubject> Subjects)
{
    public TestSubjectKey Key => new(Form.TenantId, Form.Id);
}

/// <summary>A test submission just opened, and the token the respondent page works on it with.</summary>
public sealed record OpenedTestSubmissionDto(long SubmissionId, string Token);

/// <summary>
/// Everything a test submission is made of but its definition, which <see cref="TestSubmissionWriter"/> reads once
/// it holds the form's lock.
/// </summary>
internal sealed record PreparedTestSubmission(
    TestSubject Subject,
    IReadOnlyCollection<SnapshotCell> Cells,
    Token Token,
    DateTime CapturedAt)
{
    /// <summary>
    /// No Submitter and no single-response gate: a test submission is outside one-per-person. The person's
    /// identifier is its label only. The token is on the submission before anything saves it.
    /// </summary>
    public Submission Build(long definitionId)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            Subject.TenantId,
            Subject.FormId,
            definitionId,
            "{}",
            IsComplete: false,
            SubmitterId: null,
            SubmitterDisplayId: Subject.Identifier,
            IsTestSubmission: true,
            EnforceSingleSubmissionGate: false,
            StartSubmission: true));
        submission.FreezeForTest(PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, Subject.Identifier, CapturedAt, PersonalizationSources.Test, Subject.MemberId),
            Cells));
        submission.UpdateToken(Token);
        return submission;
    }
}

/// <summary>
/// The one way to build a test submission: the form's active definition and the person's current values, flagged
/// as test. It never reads or writes Links, <c>Member.SubmitterId</c> or Submitters, so a person's personalised link
/// and one-response gate stay as they were.
/// </summary>
internal sealed class TestSubmissionOpener(
    IAudienceDbContext db,
    ISubmissionTokenService tokens,
    TestSubmissionWriter writer)
{
    /// <summary>A token that cannot be minted saves nothing.</summary>
    public async Task<Result<OpenedTestSubmissionDto>> OpenAsync(
        TestOpening opening,
        CancellationToken cancellationToken) =>
        await writer.AddAsync(await PrepareAsync(opening, cancellationToken), cancellationToken);

    /// <summary>
    /// Start over: opens a new test submission for the person and deletes <paramref name="current"/> in the same save.
    /// A token that cannot be minted saves nothing, and <paramref name="current"/> stays.
    /// </summary>
    public async Task<Result<OpenedTestSubmissionDto>> ReplaceAsync(
        TestOpening opening,
        Submission current,
        CancellationToken cancellationToken) =>
        await writer.ReplaceAsync(current, await PrepareAsync(opening, cancellationToken), cancellationToken);

    /// <summary>
    /// Opens one test submission per subject of <paramref name="batch"/>, in one save, and runs
    /// <paramref name="finish"/> on them, one per subject and in order, before anything commits. A failed
    /// <paramref name="finish"/> saves nothing.
    /// </summary>
    public async Task<Result<TResult>> OpenManyAsync<TResult>(
        TestBatch batch,
        Func<IReadOnlyList<OpenedTestSubmissionDto>, Result<TResult>> finish,
        CancellationToken cancellationToken) =>
        await writer.AddManyAsync(
            new PreparedTestBatch(batch.Key, await PrepareManyAsync(batch, cancellationToken)),
            finish,
            cancellationToken);

    /// <summary>The token lifetime is read once, and each person's values once however many runs they have.</summary>
    private async Task<List<PreparedTestSubmission>> PrepareManyAsync(TestBatch batch, CancellationToken cancellationToken)
    {
        var expiryHours = await tokens.GetExpiryHoursAsync(batch.Form.Id, batch.Form.TenantId, cancellationToken);
        var cellsByPerson = new Dictionary<long, List<SnapshotCell>>();
        var prepared = new List<PreparedTestSubmission>(batch.Subjects.Count);
        foreach (var subject in batch.Subjects)
        {
            if (!cellsByPerson.TryGetValue(subject.MembershipId, out var cells))
            {
                cells = await CellsAsync(subject, cancellationToken);
                cellsByPerson.Add(subject.MembershipId, cells);
            }

            prepared.Add(new PreparedTestSubmission(subject, cells, new Token(expiryHours), DateTime.UtcNow));
        }

        return prepared;
    }

    /// <summary>
    /// Every read an open makes but the form's: the person's values and the token. They run before the form's lock,
    /// so the locked section reads only the form, on its own connection, and never waits for a second one.
    /// </summary>
    private async Task<PreparedTestSubmission> PrepareAsync(TestOpening opening, CancellationToken cancellationToken) =>
        await PrepareAsync(opening, await CellsAsync(opening.Subject, cancellationToken), cancellationToken);

    private Task<List<SnapshotCell>> CellsAsync(TestSubject subject, CancellationToken cancellationToken) =>
        SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(subject.TenantId, subject.MembershipId, subject.FormId, subject.Identifier),
            cancellationToken);

    /// <summary>Mints the token from the form already loaded.</summary>
    internal async Task<PreparedTestSubmission> PrepareAsync(
        TestOpening opening,
        IReadOnlyCollection<SnapshotCell> cells,
        CancellationToken cancellationToken) =>
        new(opening.Subject, cells, await tokens.NewTokenAsync(opening.Form, cancellationToken), DateTime.UtcNow);
}
