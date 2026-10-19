using System.Linq.Expressions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Finds the person a test submission is opened for: an enabled form with an active definition, and the person
/// still on it. A disabled form takes no submissions, test or real.
/// </summary>
internal sealed class TestSubjects(IAudienceDbContext db, IRepository<Form> forms)
{
    public Task<Result<TestSubject>> ByMembershipAsync(long formId, long membershipId, CancellationToken cancellationToken) =>
        FindAsync(formId, membership => membership.Id == membershipId, cancellationToken);

    public Task<Result<TestSubject>> ByMemberAsync(long formId, long memberId, CancellationToken cancellationToken) =>
        FindAsync(formId, membership => membership.MemberId == memberId, cancellationToken);

    private async Task<Result<TestSubject>> FindAsync(
        long formId,
        Expression<Func<Membership, bool>> which,
        CancellationToken cancellationToken)
    {
        var form = await forms.FirstOrDefaultAsync(new OpenFormSpec(formId), cancellationToken);
        if (form is not { IsEnabled: true, DefinitionId: long definitionId })
        {
            return Result.NotFound("Form not found.");
        }

        var memberships = db.Memberships.Where(membership => membership.FormId == formId).Where(which);
        var subject = await SubjectAsync(memberships, definitionId, cancellationToken);
        return subject is null
            ? Result.NotFound("Person not found on this form.")
            : Result.Success(subject);
    }

    private Task<TestSubject?> SubjectAsync(
        IQueryable<Membership> memberships,
        long definitionId,
        CancellationToken cancellationToken) =>
        (
            from membership in memberships
            join member in db.Members on membership.MemberId equals member.Id
            select new TestSubject(
                membership.TenantId,
                membership.FormId,
                definitionId,
                membership.Id,
                member.Id,
                member.Identifier))
        .FirstOrDefaultAsync(cancellationToken);
}
