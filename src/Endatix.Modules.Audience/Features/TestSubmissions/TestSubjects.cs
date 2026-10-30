using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Finds the person a test submission is opened for: an enabled form with an active definition, and the person
/// still on it. A disabled form takes no submissions, test or real. The form is checked here before any lock, so a
/// refused open waits for nothing, and again by <see cref="TestSubmissionWriter"/> under the form's lock.
/// </summary>
/// <remarks>
/// The person comes from <see cref="LinkOpenGates.PersonAsync(IQueryable{Membership}, CancellationToken)"/>, the query
/// a link open uses. The form is loaded whole, not through <see cref="OpenFormSpec"/>: the token's expiry is read from it.
/// </remarks>
internal sealed class TestSubjects(IAudienceDbContext db, IRepository<Form> forms, LinkOpenGates gates)
{
    private const string PERSON_NOT_FOUND = "Person not found on this form.";

    private const string FORM_NOT_FOUND = "Form not found.";

    public Task<Result<TestOpening>> ByMembershipAsync(
        TestSubjectKey key,
        long membershipId,
        CancellationToken cancellationToken) =>
        FindAsync(
            key,
            FormParticipants.OnForm(db, key.TenantId, key.FormId).Where(membership => membership.Id == membershipId),
            cancellationToken);

    /// <summary>By audience member id, through <see cref="FormParticipants.ByMember"/>, as the participant panel finds them.</summary>
    public Task<Result<TestOpening>> ByMemberAsync(TestSubjectKey key, long memberId, CancellationToken cancellationToken) =>
        FindAsync(
            key,
            FormParticipants.ByMember(db, new ParticipantKey(key.TenantId, key.FormId, memberId)),
            cancellationToken);

    /// <summary>
    /// The named people on the form, or every Test participant when <paramref name="membershipIds"/> is
    /// <see langword="null"/>, in membership order, each with whether they are a Test participant. Named ids are
    /// distinct. The form is read first. Not found when the form takes no submissions or a named person is not on it.
    /// </summary>
    public async Task<Result<TestCandidates>> CandidatesAsync(
        TestSubjectKey key,
        IReadOnlyCollection<long>? membershipIds,
        CancellationToken cancellationToken)
    {
        if (await OpenFormAsync(key, cancellationToken) is not { } form)
        {
            return Result.NotFound(FORM_NOT_FOUND);
        }

        var people = await CandidateListAsync(key, membershipIds, cancellationToken);
        return membershipIds is null || people.Count == membershipIds.Count
            ? Result.Success(new TestCandidates(form, people))
            : Result.NotFound(PERSON_NOT_FOUND);
    }

    private Task<List<TestCandidate>> CandidateListAsync(
        TestSubjectKey key,
        IReadOnlyCollection<long>? membershipIds,
        CancellationToken cancellationToken)
    {
        return FormParticipants.Of(db, CandidateMemberships(key, membershipIds))
            .OrderBy(participant => participant.Membership.Id)
            .Select(participant => new TestCandidate(
                new TestSubject(
                    participant.Membership.TenantId,
                    key.FormId,
                    participant.Membership.Id,
                    participant.Member.Id,
                    participant.Member.Identifier),
                participant.Membership.IsTest))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Membership> CandidateMemberships(TestSubjectKey key, IReadOnlyCollection<long>? membershipIds)
    {
        var onForm = FormParticipants.OnForm(db, key.TenantId, key.FormId);
        return membershipIds is null
            ? onForm.Where(membership => membership.IsTest)
            : onForm.Where(membership => membershipIds.Contains(membership.Id));
    }

    private async Task<Result<TestOpening>> FindAsync(
        TestSubjectKey key,
        IQueryable<Membership> membership,
        CancellationToken cancellationToken)
    {
        if (await OpenFormAsync(key, cancellationToken) is not { } form)
        {
            return Result.NotFound(FORM_NOT_FOUND);
        }

        var person = await gates.PersonAsync(membership, cancellationToken);
        return person is null
            ? Result.NotFound(PERSON_NOT_FOUND)
            : Result.Success(new TestOpening(
                new TestSubject(person.TenantId, key.FormId, person.MembershipId, person.MemberId, person.Identifier),
                form));
    }

    private async Task<Form?> OpenFormAsync(TestSubjectKey key, CancellationToken cancellationToken)
    {
        var form = await forms.FirstOrDefaultAsync(new FormSpecifications.ByIdReadOnly(key.FormId), cancellationToken);
        return form is { IsEnabled: true, ActiveDefinitionId: not null } && form.TenantId == key.TenantId ? form : null;
    }
}

/// <summary>The people <see cref="TestSubjects.CandidatesAsync"/> found, and their form.</summary>
internal sealed record TestCandidates(Form Form, IReadOnlyList<TestCandidate> People);

/// <summary>A person on the form; <see cref="TestSubjects"/> finds real participants too.</summary>
internal sealed record TestCandidate(TestSubject Subject, bool IsTest);

/// <summary>The tenant and form a test submission is opened on. Both are stated in every read.</summary>
internal sealed record TestSubjectKey(long TenantId, long FormId);
