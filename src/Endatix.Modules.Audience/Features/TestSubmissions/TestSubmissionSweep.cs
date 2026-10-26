using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>The participants whose test submissions on one form are counted or deleted, by audience member id.</summary>
internal sealed record TestSweepScope(long TenantId, long FormId, IReadOnlyCollection<long> MemberIds);

/// <summary>
/// Counts or deletes the test submissions opened as some participants of a form (<see cref="TestSubmissionsOfSpec"/>),
/// as <c>DeleteTestSubmissionsHandler</c> deletes a whole form's: through the aggregate, so each raises
/// <c>submission.deleted</c> and Reporting drops its row, in keyset pages that each commit on their own.
/// </summary>
/// <remarks>
/// The member ids are sent <see cref="MEMBER_CHUNK"/> at a time, each one a query parameter. A failure part way leaves
/// the earlier pages deleted; a retry carries on, since deleted submissions no longer match.
/// <see cref="BaseEntity.IsDeleted"/> is a concurrency token, so a page with a row deleted since it was read saves
/// nothing and is read again from the same <c>afterId</c>, a bounded number of times.
/// </remarks>
internal sealed class TestSubmissionSweep(IRepository<Submission> submissions, IUnitOfWork unitOfWork)
{
    /// <summary>Submissions loaded and saved per round trip.</summary>
    internal const int PAGE_SIZE = 500;

    /// <summary>Member ids per query, well under SQL Server's 2,100 parameters.</summary>
    internal const int MEMBER_CHUNK = 500;

    /// <summary>How many times a page is read and saved before the sweep gives up as busy.</summary>
    internal const int PAGE_ATTEMPTS = 3;

    private const string BUSY_MESSAGE = "The participants' test submissions are being changed; try again shortly.";

    public async Task<int> CountAsync(TestSweepScope scope, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var memberIds in scope.MemberIds.Chunk(MEMBER_CHUNK))
        {
            total += await submissions.CountAsync(
                new TestSubmissionsOfSpec(new(scope.TenantId, scope.FormId, memberIds, AfterId: 0), PAGE_SIZE),
                cancellationToken);
        }

        return total;
    }

    /// <returns>How many were deleted; <see cref="ResultStatus.Unavailable"/> when a page kept losing to other writes.</returns>
    public async Task<Result<int>> DeleteAsync(TestSweepScope scope, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var memberIds in scope.MemberIds.Chunk(MEMBER_CHUNK))
        {
            var deleted = await DeleteChunkAsync(new(scope.TenantId, scope.FormId, memberIds, AfterId: 0), cancellationToken);
            if (!deleted.IsSuccess)
            {
                return deleted;
            }

            total += deleted.Value;
        }

        return total;
    }

    private async Task<Result<int>> DeleteChunkAsync(TestSubmissionsOfKey start, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var next = start;
        while (true)
        {
            var page = await DeletePageAsync(next, cancellationToken);
            if (!page.IsSuccess)
            {
                return page.ToErrorResult<int>();
            }

            deleted += page.Value.Count;
            if (page.Value.Count < PAGE_SIZE)
            {
                return deleted;
            }

            next = next with { AfterId = page.Value.LastId };
        }
    }

    private async Task<Result<DeletedPage>> DeletePageAsync(TestSubmissionsOfKey start, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < PAGE_ATTEMPTS; attempt++)
        {
            var page = await TryDeletePageAsync(start, cancellationToken);
            if (page.Status != ResultStatus.NotFound)
            {
                return page;
            }
        }

        return Result.Unavailable(BUSY_MESSAGE);
    }

    /// <summary>
    /// Not found when a row of the page was deleted since it was read; unavailable when the save lost a deadlock.
    /// Either way the page saved nothing.
    /// </summary>
    private async Task<Result<DeletedPage>> TryDeletePageAsync(TestSubmissionsOfKey start, CancellationToken cancellationToken)
    {
        var page = await submissions.ListAsync(new TestSubmissionsOfSpec(start, PAGE_SIZE), cancellationToken);
        if (page.Count == 0)
        {
            return Result.Success(new DeletedPage(0, start.AfterId));
        }

        foreach (var submission in page)
        {
            submission.Delete();
        }

        var saved = await submissions.TrySaveChangesAsync(cancellationToken);
        unitOfWork.ClearChangeTracker();
        return saved.IsSuccess
            ? Result.Success(new DeletedPage(page.Count, page[^1].Id))
            : saved.ToErrorResult<DeletedPage>();
    }

    /// <summary>How many submissions a page deleted, and the last id it read.</summary>
    private sealed record DeletedPage(int Count, long LastId);
}
