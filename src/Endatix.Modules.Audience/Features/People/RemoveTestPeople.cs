using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.TestSubmissions;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Removes test participants from a form, and with <see cref="AlsoDeleteTestSubmissions"/> the test submissions opened
/// as them. <see cref="MembershipIds"/> null means every test participant on the form. With <see cref="DryRun"/> it
/// counts and writes nothing.
/// </summary>
public sealed record RemoveTestPeopleCommand(
    long TenantId,
    long FormId,
    IReadOnlyList<long>? MembershipIds,
    bool AlsoDeleteTestSubmissions,
    bool DryRun) : ICommand<Result<RemovedTestPeopleDto>>;

/// <summary>What was removed, or with <see cref="DryRun"/> what would be.</summary>
public sealed record RemovedTestPeopleDto(int People, int TestSubmissions, bool DryRun);

/// <summary>
/// People first, one at a time through <see cref="PersonRemoval"/> under each one's removal locks, then their test
/// submissions through <see cref="TestSubmissionSweep"/>. Every step is idempotent, so the same request again finishes
/// a run that failed part way: the targets are read with their tombstones, and the sweep covers removed ones too.
/// </summary>
/// <remarks>
/// <para>
/// The test submissions are found by the audience member id their Snapshot names, so the sweep leaves out a member
/// who is on the form again as a real participant: a real participant never loses a submission. A removal answers 404
/// to a new test open as that person, but an open that found the person before their removal and saves after the sweep
/// leaves one test submission behind, whose link works until it expires.
/// </para>
/// <para>
/// Deleting test submissions needs <see cref="Actions.Forms.Test"/> and <see cref="Actions.Submissions.Delete"/>, the
/// pair <c>POST submissions/test/delete</c> needs, checked here because the route needs only
/// <see cref="Actions.Forms.Edit"/>. A dry run asks for them too, so it answers as the removal would.
/// </para>
/// </remarks>
internal sealed class RemoveTestPeopleHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    MembershipLock membershipLock,
    TestSubmissionSweep sweep,
    ICurrentUserAuthorizationService authorization)
    : ICommandHandler<RemoveTestPeopleCommand, Result<RemovedTestPeopleDto>>
{
    internal const string REAL_PARTICIPANT_MESSAGE =
        "Only test participants can be removed this way; remove a real participant on their own.";

    private const string NOT_ON_FORM_MESSAGE = "Audience membership not found.";

    public async Task<Result<RemovedTestPeopleDto>> Handle(
        RemoveTestPeopleCommand request,
        CancellationToken cancellationToken)
    {
        Result allowed = await GateAsync(request, cancellationToken);
        if (!allowed.IsSuccess)
        {
            return allowed.ToErrorResult<RemovedTestPeopleDto>();
        }

        Result<List<Target>> targets = await TargetsAsync(request, cancellationToken);
        if (!targets.IsSuccess)
        {
            return targets.ToErrorResult<RemovedTestPeopleDto>();
        }

        return request.DryRun
            ? await CountAsync(request, targets.Value, cancellationToken)
            : await RemoveAsync(request, targets.Value, cancellationToken);
    }

    private async Task<Result> GateAsync(RemoveTestPeopleCommand request, CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess || !request.AlsoDeleteTestSubmissions)
        {
            return gate;
        }

        Result mayTest = await authorization.ValidateAccessAsync(Actions.Forms.Test, cancellationToken);
        return mayTest.IsSuccess
            ? await authorization.ValidateAccessAsync(Actions.Submissions.Delete, cancellationToken)
            : mayTest;
    }

    /// <summary>
    /// The chosen memberships with their tombstones, so a retry finds the ones it removed: 404 when one is not on the
    /// form, 400 when one is a real participant.
    /// </summary>
    private async Task<Result<List<Target>>> TargetsAsync(
        RemoveTestPeopleCommand request,
        CancellationToken cancellationToken)
    {
        IQueryable<Membership> onForm = db.Memberships
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(membership => membership.TenantId == request.TenantId && membership.FormId == request.FormId);
        IReadOnlyList<long>? ids = request.MembershipIds;
        IQueryable<Membership> chosen = ids is null
            ? onForm.Where(membership => membership.IsTest)
            : onForm.Where(membership => ids.Contains(membership.Id));
        List<Target> targets = await chosen
            .Select(membership => new Target(
                membership.Id,
                membership.MemberId,
                membership.IsTest,
                membership.IsDeleted,
                db.Memberships.Any(live =>
                    live.FormId == membership.FormId && live.MemberId == membership.MemberId && !live.IsTest)))
            .ToListAsync(cancellationToken);

        if (ids is not null && targets.Count != ids.Count)
        {
            return Result.NotFound(NOT_ON_FORM_MESSAGE);
        }

        return targets.TrueForAll(target => target.IsTest)
            ? targets
            : Result.Invalid(new ValidationError(REAL_PARTICIPANT_MESSAGE));
    }

    private async Task<Result<RemovedTestPeopleDto>> CountAsync(
        RemoveTestPeopleCommand request,
        List<Target> targets,
        CancellationToken cancellationToken)
    {
        int testSubmissions = request.AlsoDeleteTestSubmissions
            ? await sweep.CountAsync(SweepOf(request, targets), cancellationToken)
            : 0;
        return new RemovedTestPeopleDto(targets.Count(target => !target.IsRemoved), testSubmissions, DryRun: true);
    }

    private async Task<Result<RemovedTestPeopleDto>> RemoveAsync(
        RemoveTestPeopleCommand request,
        List<Target> targets,
        CancellationToken cancellationToken)
    {
        Result<int> people = await RemoveEachAsync(request.FormId, targets, cancellationToken);
        if (!people.IsSuccess || !request.AlsoDeleteTestSubmissions)
        {
            return people.Map(removed => new RemovedTestPeopleDto(removed, 0, DryRun: false));
        }

        Result<int> testSubmissions = await sweep.DeleteAsync(SweepOf(request, targets), cancellationToken);
        return testSubmissions.Map(deleted => new RemovedTestPeopleDto(people.Value, deleted, DryRun: false));
    }

    /// <summary>
    /// One participant at a time, each in a transaction of its own under their removal locks. One removed meanwhile is
    /// skipped; a lock that stays held stops the run, and the same request again carries on.
    /// </summary>
    private async Task<Result<int>> RemoveEachAsync(long formId, List<Target> targets, CancellationToken cancellationToken)
    {
        PersonRemoval removal = new(db, membershipLock);
        var removed = 0;
        foreach (Target target in targets.Where(target => !target.IsRemoved).OrderBy(target => target.MembershipId))
        {
            Result<Membership> result = await removal.ApplyAsync(new(formId, target.MembershipId), cancellationToken);
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

    private static TestSweepScope SweepOf(RemoveTestPeopleCommand request, List<Target> targets) =>
        new(
            request.TenantId,
            request.FormId,
            targets.Where(target => !target.IsRealNow).Select(target => target.MemberId).Distinct().ToList());

    /// <summary>A chosen membership: whether it is a test one, removed already, and its member on the form as real now.</summary>
    private sealed record Target(long MembershipId, long MemberId, bool IsTest, bool IsRemoved, bool IsRealNow);
}
