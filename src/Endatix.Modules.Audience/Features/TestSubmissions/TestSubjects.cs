using System.Linq.Expressions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;

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
    public Task<Result<TestOpening>> ByMembershipAsync(
        TestSubjectKey key,
        long membershipId,
        CancellationToken cancellationToken) =>
        FindAsync(key, membership => membership.Id == membershipId, cancellationToken);

    public Task<Result<TestOpening>> ByMemberAsync(TestSubjectKey key, long memberId, CancellationToken cancellationToken) =>
        FindAsync(key, membership => membership.MemberId == memberId, cancellationToken);

    private async Task<Result<TestOpening>> FindAsync(
        TestSubjectKey key,
        Expression<Func<Membership, bool>> which,
        CancellationToken cancellationToken)
    {
        var form = await forms.FirstOrDefaultAsync(new FormSpecifications.ByIdReadOnly(key.FormId), cancellationToken);
        if (form is not { IsEnabled: true, ActiveDefinitionId: not null } || form.TenantId != key.TenantId)
        {
            return Result.NotFound("Form not found.");
        }

        var person = await PersonAsync(key, which, cancellationToken);
        return person is null
            ? Result.NotFound("Person not found on this form.")
            : Result.Success(new TestOpening(
                new TestSubject(person.TenantId, key.FormId, person.MembershipId, person.MemberId, person.Identifier),
                form));
    }

    private Task<LinkPerson?> PersonAsync(
        TestSubjectKey key,
        Expression<Func<Membership, bool>> which,
        CancellationToken cancellationToken) =>
        gates.PersonAsync(
            db.Memberships
                .Where(membership => membership.TenantId == key.TenantId && membership.FormId == key.FormId)
                .Where(which),
            cancellationToken);
}

/// <summary>The tenant and form a test submission is opened on. Both are stated in every read.</summary>
internal sealed record TestSubjectKey(long TenantId, long FormId);
