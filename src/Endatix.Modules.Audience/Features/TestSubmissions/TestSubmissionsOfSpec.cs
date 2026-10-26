using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Modules.Audience.Features.Links;
using static Endatix.Modules.Audience.Features.Links.SnapshotExpression;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>Whose test submissions to page through: the participants' audience member ids, and where the page starts.</summary>
internal sealed record TestSubmissionsOfKey(long TenantId, long FormId, IReadOnlyCollection<long> MemberIds, long AfterId);

/// <summary>
/// One keyset page of the test submissions opened as these participants on one form: a test submission whose Snapshot
/// is from a test open (<see cref="PersonalizationSnapshotJson.IsTest"/>) and names one of them
/// (<see cref="PersonalizationSnapshotJson.NamesAnyParticipant"/>), after <see cref="TestSubmissionsOfKey.AfterId"/>,
/// by id. A submission marked as test from the submissions page has no such Snapshot and is left alone. Deleted
/// submissions drop by the query filter. Tracked, so the page can be deleted and saved.
/// </summary>
internal sealed class TestSubmissionsOfSpec : Specification<Submission>
{
    public TestSubmissionsOfSpec(TestSubmissionsOfKey key, int take)
    {
        Query.Where(Theirs(key))
            .OrderBy(submission => submission.Id)
            .Take(take);
    }

    private static Expression<Func<Submission, bool>> Theirs(TestSubmissionsOfKey key)
    {
        Expression<Func<Submission, bool>> page = submission =>
            submission.TenantId == key.TenantId
            && submission.FormId == key.FormId
            && submission.IsTestSubmission
            && submission.Id > key.AfterId;
        Expression<Func<string?, bool>> isTest = PersonalizationSnapshotJson.IsTest();
        Expression<Func<string?, bool>> namesThem = PersonalizationSnapshotJson.NamesAnyParticipant(key.MemberIds);

        ParameterExpression submission = page.Parameters[0];
        Expression snapshot = Expression.Property(submission, nameof(Submission.PersonalizationSnapshot));
        Expression body = Expression.AndAlso(
            page.Body,
            Expression.AndAlso(BodyOf(isTest, snapshot), BodyOf(namesThem, snapshot)));
        return Expression.Lambda<Func<Submission, bool>>(body, submission);
    }
}
