using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for a form's settings. A lost race is conceded, not thrown, the way <c>TenantBrandingRepository</c>
/// concedes one.
/// </summary>
/// <remarks>
/// Two writes made from the same version must not both win. The database decides, not a lock: the
/// <see cref="FormSettings.Version"/> concurrency token makes an update from an older version change no row, and the
/// key refuses a second first row.
/// </remarks>
internal sealed class FormSettingsRepository(
    AppDbContext dbContext,
    IUniqueConstraintViolationChecker uniqueViolationChecker) : IFormSettingsRepository
{
    /// <inheritdoc />
    public async Task<FormSettings> ReadAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        await dbContext.FormSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.FormId == formId, cancellationToken)
        ?? new FormSettings(tenantId, formId);

    /// <inheritdoc />
    public Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken) =>
        dbContext.FormSettings
            .FirstOrDefaultAsync(settings => settings.FormId == formId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(settings).State == EntityState.Detached)
        {
            dbContext.Add(settings);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsLostRace(exception))
        {
            // A refused write leaves nothing tracked, so a later save in the same scope does not replay it
            // (ARCHITECTURE.md, transaction locks, item 8).
            dbContext.Entry(settings).State = EntityState.Detached;
            return false;
        }
    }

    // A stale Version matches no row; a taken form key is a unique violation.
    private bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || uniqueViolationChecker.AnalyzeUniqueConstraint(exception).IsUniqueConstraintViolation;
}
