using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for a form's settings. A lost race is conceded, not thrown, the way <c>TenantBrandingRepository</c>
/// concedes one.
/// </summary>
/// <remarks>
/// Two writes made from the same version must not both win. The database decides, not a lock: the
/// <see cref="FormSettings.Version"/> concurrency token makes an update from an older version change no row, and the
/// key refuses a second first row. A form save writes the settings too (<see cref="FormWebHookSettingsMirror"/>), so it
/// can lose the same race.
/// </remarks>
internal sealed class FormSettingsRepository(
    AppDbContext dbContext,
    IRepository<Form> forms,
    IUniqueConstraintViolationChecker uniqueViolationChecker) : IFormSettingsRepository
{
    private const string FORM_SETTINGS_KEY_NAME = "PK_FormSettings";

    /// <inheritdoc />
    public async Task<FormSettings> ReadAsync(Form form, CancellationToken cancellationToken) =>
        await dbContext.FormSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.FormId == form.Id && settings.TenantId == form.TenantId, cancellationToken)
        ?? FormSettings.For(form);

    /// <inheritdoc />
    public Task<FormSettings?> FindAsync(long formId, CancellationToken cancellationToken) =>
        dbContext.FormSettings
            .FirstOrDefaultAsync(settings => settings.FormId == formId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SaveOrConcedeAsync(FormSettings settings, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(settings).State == EntityState.Detached)
        {
            dbContext.Add(settings);
        }

        return ConcedeLostRaceAsync(settings.FormId, () => dbContext.SaveChangesAsync(cancellationToken));
    }

    /// <inheritdoc />
    public Task<bool> UpdateFormOrConcedeAsync(Form form, CancellationToken cancellationToken) =>
        ConcedeLostRaceAsync(form.Id, () => forms.UpdateAsync(form, cancellationToken));

    private async Task<bool> ConcedeLostRaceAsync(long formId, Func<Task> save)
    {
        try
        {
            await save();
            return true;
        }
        catch (DbUpdateException exception) when (IsLostRace(exception))
        {
            DetachRefused(formId);
            return false;
        }
    }

    // A stale Version matches no row; a second first row is a unique violation of the key.
    private bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException concurrency
            ? concurrency.Entries.Any(entry => entry.Entity is FormSettings)
            : uniqueViolationChecker.AnalyzeUniqueConstraint(exception) is { IsUniqueConstraintViolation: true } violation
              && string.Equals(violation.ConstraintName, FORM_SETTINGS_KEY_NAME, StringComparison.Ordinal);

    // The whole save rolled back, so none of its pending changes was stored: the settings, the form whose webhooks
    // column it wrote, and the outbox rows captured with them. A refused write leaves nothing tracked, so a later save
    // in the same scope does not replay it (ARCHITECTURE.md, transaction locks, item 8). The form goes even when it is
    // unchanged, so the caller reads the winner's form again.
    private void DetachRefused(long formId)
    {
        var refused = dbContext.ChangeTracker.Entries().Where(IsPending).ToList();
        if (dbContext.Forms.Local.FirstOrDefault(form => form.Id == formId) is { } trackedForm)
        {
            refused.Add(dbContext.Entry(trackedForm));
        }

        foreach (var entry in refused)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsPending(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
}
