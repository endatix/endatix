using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>Which test participants of a form to remove: <see cref="MembershipIds"/> null means every one.</summary>
internal sealed record TestTargetsKey(long TenantId, long FormId, IReadOnlyCollection<long>? MembershipIds);

/// <summary>A named membership, read with its tombstone: whether it is a test one, and whether it is removed already.</summary>
internal sealed record TestTarget(long MembershipId, long MemberId, bool IsTest, bool IsRemoved);

/// <summary>
/// The people half of removing test participants: finds them with their tombstones, so the same request again finds
/// the ones it removed, removes the live ones one at a time, and says whose test submissions may then go.
/// </summary>
internal sealed class TestParticipantRemoval(IAudienceDbContext db, MembershipLock membershipLock)
{
    internal const string REAL_PARTICIPANT_MESSAGE =
        "Only test participants can be removed this way; remove a real participant on their own.";

    private const string NOT_ON_FORM_MESSAGE = "Audience membership not found.";

    /// <returns>The targets; 404 when a named membership was never on the form; 400 when one is a real participant.</returns>
    public async Task<Result<List<TestTarget>>> TargetsAsync(TestTargetsKey key, CancellationToken cancellationToken)
    {
        List<TestTarget> targets = await Chosen(key)
            .Select(membership => new TestTarget(membership.Id, membership.MemberId, membership.IsTest, membership.IsDeleted))
            .ToListAsync(cancellationToken);

        if (key.MembershipIds is not null && targets.Count != key.MembershipIds.Count)
        {
            return Result.NotFound(NOT_ON_FORM_MESSAGE);
        }

        return targets.TrueForAll(target => target.IsTest)
            ? targets
            : Result.Invalid(new ValidationError(REAL_PARTICIPANT_MESSAGE));
    }

    /// <summary>
    /// One live target at a time, each in a transaction of its own under their removal locks (<see cref="PersonRemoval"/>).
    /// One removed meanwhile is skipped; a lock that stays held stops the run, and the same request again carries on.
    /// </summary>
    /// <returns>How many this call removed.</returns>
    public async Task<Result<int>> RemoveAsync(long formId, IEnumerable<TestTarget> targets, CancellationToken cancellationToken)
    {
        PersonRemoval removal = new(db, membershipLock);
        var removed = 0;
        foreach (TestTarget target in targets.Where(target => !target.IsRemoved).OrderBy(target => target.MembershipId))
        {
            Result<Membership> result = await removal.ApplyAsync(new(formId, target.MembershipId), cancellationToken);
            ((DbContext)db).ChangeTracker.Clear();
            if (result.IsSuccess)
            {
                removed++;
            }
            else if (result.Status != ResultStatus.NotFound)
            {
                return result.ToErrorResult<int>();
            }
        }

        return removed;
    }

    /// <summary>
    /// The targets' audience members whose test submissions may go: not one still on the form through a membership that
    /// is not a target, real or test. A Snapshot names the member, not the membership, so such a member's test
    /// submissions can't be told apart and all stay: a real participant never loses one, and a test participant the
    /// request did not name keeps their links. Read after the removals, so it sees who is on the form now.
    /// </summary>
    public async Task<List<long>> SweptMemberIdsAsync(
        TestTargetsKey key,
        IReadOnlyCollection<TestTarget> targets,
        CancellationToken cancellationToken)
    {
        HashSet<long> targetIds = targets.Select(target => target.MembershipId).ToHashSet();
        List<long> members = targets.Select(target => target.MemberId).Distinct().ToList();
        HashSet<long> stillOnForm = [];
        foreach (long[] chunk in members.Chunk(TestSubmissionSweep.MEMBER_CHUNK))
        {
            List<TestTarget> live = await LiveMembershipsAsync(key, chunk, cancellationToken);
            stillOnForm.UnionWith(live.Where(row => !targetIds.Contains(row.MembershipId)).Select(row => row.MemberId));
        }

        return members.Where(member => !stillOnForm.Contains(member)).ToList();
    }

    /// <summary>The live memberships on the form of these audience members.</summary>
    private Task<List<TestTarget>> LiveMembershipsAsync(
        TestTargetsKey key,
        long[] memberIds,
        CancellationToken cancellationToken) =>
        db.Memberships
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && memberIds.Contains(membership.MemberId))
            .Select(membership => new TestTarget(membership.Id, membership.MemberId, membership.IsTest, false))
            .ToListAsync(cancellationToken);

    private IQueryable<Membership> Chosen(TestTargetsKey key)
    {
        IQueryable<Membership> onForm = db.Memberships
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(membership => membership.TenantId == key.TenantId && membership.FormId == key.FormId);
        IReadOnlyCollection<long>? ids = key.MembershipIds;
        return ids is null
            ? onForm.Where(membership => membership.IsTest)
            : onForm.Where(membership => ids.Contains(membership.Id));
    }
}
