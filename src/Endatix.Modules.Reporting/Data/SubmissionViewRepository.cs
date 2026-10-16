using Endatix.Core.Abstractions.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for saved submissions grid views.
/// </summary>
/// <remarks>
/// Two saves made from the same revision must not both win. The database decides, not a lock: the unique index
/// refuses a second create, and the <see cref="SubmissionView.Revision"/> concurrency token makes an update from an
/// older revision change no row.
/// </remarks>
internal sealed class SubmissionViewRepository(
    IReportingDbContext dbContext,
    IUniqueConstraintViolationChecker uniqueViolationChecker) : ISubmissionViewRepository
{
    /// <inheritdoc />
    public Task<SubmissionView?> FindSharedAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        SharedView(tenantId, formId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<SubmissionView?> SaveSharedAsync(SubmissionViewSave save, CancellationToken cancellationToken)
    {
        var view = await SharedView(save.TenantId, save.FormId).FirstOrDefaultAsync(cancellationToken);
        if (view is null)
        {
            return save.Revision == SubmissionView.UnsavedRevision
                ? await CreateAsync(save, cancellationToken)
                : null;
        }

        if (view.Revision != save.Revision)
        {
            return null;
        }

        view.UpdateSettings(save.SettingsJson, save.ModifiedBy);
        return await SaveOrConcedeAsync(view, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        dbContext.SubmissionViews
            .IgnoreQueryFilters()
            .Where(view => view.TenantId == tenantId && view.FormId == formId)
            .ExecuteDeleteAsync(cancellationToken);

    private Task<SubmissionView?> CreateAsync(SubmissionViewSave save, CancellationToken cancellationToken)
    {
        var view = SubmissionView.CreateShared(
            new SubmissionViewCreateArgs(save.TenantId, save.FormId, save.SettingsJson, save.ModifiedBy));
        dbContext.SubmissionViews.Add(view);
        return SaveOrConcedeAsync(view, cancellationToken);
    }

    private async Task<SubmissionView?> SaveOrConcedeAsync(SubmissionView view, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return view;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.SubmissionViews.Entry(view).State = EntityState.Detached;
            return null;
        }
        catch (DbUpdateException exception)
            when (uniqueViolationChecker.AnalyzeUniqueConstraint(exception).IsUniqueConstraintViolation)
        {
            dbContext.SubmissionViews.Entry(view).State = EntityState.Detached;
            return null;
        }
    }

    private IQueryable<SubmissionView> SharedView(long tenantId, long formId) =>
        dbContext.SubmissionViews.Where(view =>
            view.TenantId == tenantId &&
            view.FormId == formId &&
            view.OwnerUserId == SubmissionView.SharedOwnerUserId);
}
