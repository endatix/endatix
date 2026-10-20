using Endatix.Core.Abstractions.Data;
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
/// </remarks>
public sealed class DeleteTestSubmissionsHandler(
    IRepository<Submission> submissions,
    IRepository<Form> forms,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTestSubmissionsCommand, Result<DeletedTestSubmissions>>
{
    /// <summary>Submissions loaded and saved per round trip, which bounds each query and each save.</summary>
    internal const int PAGE_SIZE = 500;

    /// <inheritdoc />
    public async Task<Result<DeletedTestSubmissions>> Handle(DeleteTestSubmissionsCommand request, CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        if (await forms.FirstOrDefaultAsync(new FormSpecifications.ByIdForTenant(request.FormId, request.TenantId), cancellationToken) is null)
        {
            return Result.NotFound("Form not found");
        }

        return Result.Success(new DeletedTestSubmissions(await DeleteAllAsync(request.TenantId, request.FormId, cancellationToken)));
    }

    private async Task<int> DeleteAllAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var deleted = 0;
        long afterId = 0;
        while (true)
        {
            var page = await submissions.ListAsync(new TestSubmissionsByFormIdPageSpec(tenantId, formId, afterId, PAGE_SIZE), cancellationToken);
            if (page.Count == 0)
            {
                return deleted;
            }

            foreach (var submission in page)
            {
                submission.Delete();
            }

            await submissions.SaveChangesAsync(cancellationToken);
            unitOfWork.ClearChangeTracker();
            deleted += page.Count;
            if (page.Count < PAGE_SIZE)
            {
                return deleted;
            }

            afterId = page[^1].Id;
        }
    }
}
