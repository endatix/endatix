using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.TestSubmissions;

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
/// People first, through <see cref="TestParticipantRemoval"/>, then their test submissions through
/// <see cref="TestSubmissionSweep"/>. Every step is idempotent, so the same request again finishes a run that failed
/// part way.
/// </summary>
/// <remarks>
/// <para>
/// A removal answers 404 to a new test open as that person, but an open that found the person before their removal and
/// saves after the sweep leaves one test submission behind, whose link works until it expires.
/// </para>
/// <para>
/// Deleting test submissions needs <see cref="Actions.Forms.Test"/> and <see cref="Actions.Submissions.Delete"/>, the
/// pair <c>POST submissions/test/delete</c> needs, checked here because the route needs only
/// <see cref="Actions.Forms.Edit"/>. A dry run asks for them too, so it answers as the removal would.
/// </para>
/// </remarks>
internal sealed class RemoveTestPeopleHandler(
    IRepository<Form> forms,
    TestParticipantRemoval testParticipants,
    TestSubmissionSweep sweep,
    ICurrentUserAuthorizationService authorization)
    : ICommandHandler<RemoveTestPeopleCommand, Result<RemovedTestPeopleDto>>
{
    public async Task<Result<RemovedTestPeopleDto>> Handle(
        RemoveTestPeopleCommand request,
        CancellationToken cancellationToken)
    {
        Result allowed = await GateAsync(request, cancellationToken);
        if (!allowed.IsSuccess)
        {
            return allowed.ToErrorResult<RemovedTestPeopleDto>();
        }

        Result<List<TestTarget>> targets = await testParticipants.TargetsAsync(KeyOf(request), cancellationToken);
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

    private async Task<Result<RemovedTestPeopleDto>> CountAsync(
        RemoveTestPeopleCommand request,
        List<TestTarget> targets,
        CancellationToken cancellationToken)
    {
        int testSubmissions = request.AlsoDeleteTestSubmissions
            ? await sweep.CountAsync(await SweepScopeAsync(request, targets, cancellationToken), cancellationToken)
            : 0;
        return new RemovedTestPeopleDto(targets.Count(target => !target.IsRemoved), testSubmissions, DryRun: true);
    }

    private async Task<Result<RemovedTestPeopleDto>> RemoveAsync(
        RemoveTestPeopleCommand request,
        List<TestTarget> targets,
        CancellationToken cancellationToken)
    {
        Result<int> people = await testParticipants.RemoveAsync(request.FormId, targets, cancellationToken);
        if (!people.IsSuccess || !request.AlsoDeleteTestSubmissions)
        {
            return people.Map(removed => new RemovedTestPeopleDto(removed, 0, DryRun: false));
        }

        TestSweepScope scope = await SweepScopeAsync(request, targets, cancellationToken);
        Result<int> testSubmissions = await sweep.DeleteAsync(scope, cancellationToken);
        return testSubmissions.Map(deleted => new RemovedTestPeopleDto(people.Value, deleted, DryRun: false));
    }

    private async Task<TestSweepScope> SweepScopeAsync(
        RemoveTestPeopleCommand request,
        List<TestTarget> targets,
        CancellationToken cancellationToken) =>
        new(
            request.TenantId,
            request.FormId,
            await testParticipants.SweptMemberIdsAsync(KeyOf(request), targets, cancellationToken));

    private static TestTargetsKey KeyOf(RemoveTestPeopleCommand request) =>
        new(request.TenantId, request.FormId, request.MembershipIds);
}
