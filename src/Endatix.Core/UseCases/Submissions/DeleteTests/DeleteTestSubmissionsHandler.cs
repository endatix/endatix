using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.DeleteTests;

/// <summary>
/// The endpoint requires <c>forms.test</c> and <c>submissions.delete</c>. Form and submissions are read for the caller's
/// tenant, not through the ambient filter. Each submission is deleted through the aggregate, so every one raises
/// <c>submission.deleted</c> and Reporting drops its flattened row.
/// </summary>
/// <remarks>
/// Each page commits on its own: a failure part way leaves the earlier pages deleted, and a retry carries on, since
/// deleted submissions no longer match. The change tracker is cleared after each page, so a form with many test
/// submissions holds one page in memory, not all of them. The keyset (<c>afterId</c>) stays although deleted rows drop
/// out of the query anyway: a row whose delete did not take would otherwise be read again on every page, for ever.
/// <see cref="BaseEntity.IsDeleted"/> is a concurrency token, so a page with a row deleted since it was read saves
/// nothing. The page is read again from the same <c>afterId</c>, a bounded number of times, unless the form itself
/// was deleted meanwhile, which is not found.
/// </remarks>
public sealed class DeleteTestSubmissionsHandler(
    IRepository<Submission> submissions,
    IRepository<Form> forms,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTestSubmissionsCommand, Result<DeletedTestSubmissions>>
{
    /// <summary>Submissions loaded and saved per round trip, which bounds each query and each save.</summary>
    internal const int PAGE_SIZE = 500;

    /// <summary>How many times a page is read and saved before the request gives up as busy.</summary>
    internal const int PAGE_ATTEMPTS = 3;

    private const string FORM_NOT_FOUND_MESSAGE = "Form not found";

    private const string BUSY_MESSAGE = "The form's test submissions are being changed; try again shortly.";

    /// <inheritdoc />
    public async Task<Result<DeletedTestSubmissions>> Handle(DeleteTestSubmissionsCommand request, CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        if (!await FormExistsAsync(request.TenantId, request.FormId, cancellationToken))
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        var deleted = await DeleteAllAsync(request.TenantId, request.FormId, cancellationToken);
        return deleted.IsSuccess
            ? Result.Success(new DeletedTestSubmissions(deleted.Value))
            : deleted.ToErrorResult<DeletedTestSubmissions>();
    }

    private async Task<Result<int>> DeleteAllAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var next = new PageStart(tenantId, formId, AfterId: 0);
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

    /// <summary>
    /// Deletes the page after <see cref="PageStart.AfterId"/>, read again when a row of it was deleted meanwhile:
    /// not found once the form is gone, unavailable when every attempt lost a row.
    /// </summary>
    private async Task<Result<DeletedPage>> DeletePageAsync(PageStart start, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < PAGE_ATTEMPTS; attempt++)
        {
            var page = await TryDeletePageAsync(start, cancellationToken);
            if (page.Status != ResultStatus.NotFound)
            {
                return page;
            }

            if (!await FormExistsAsync(start.TenantId, start.FormId, cancellationToken))
            {
                return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
            }
        }

        return Result.Unavailable(BUSY_MESSAGE);
    }

    /// <summary>
    /// Not found when a row of the page was deleted since it was read; unavailable when the save lost a deadlock.
    /// Either way the page saved nothing.
    /// </summary>
    private async Task<Result<DeletedPage>> TryDeletePageAsync(PageStart start, CancellationToken cancellationToken)
    {
        var page = await submissions.ListAsync(
            new TestSubmissionsByFormIdPageSpec(start.TenantId, start.FormId, start.AfterId, PAGE_SIZE),
            cancellationToken);
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

    private async Task<bool> FormExistsAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        await forms.FirstOrDefaultAsync(new FormSpecifications.ByIdForTenant(formId, tenantId), cancellationToken) is not null;

    /// <summary>Where the next page starts: after <paramref name="AfterId"/>, or at the start for 0.</summary>
    private sealed record PageStart(long TenantId, long FormId, long AfterId);

    /// <summary>How many submissions a page deleted, and the last id it read.</summary>
    private sealed record DeletedPage(int Count, long LastId);
}
