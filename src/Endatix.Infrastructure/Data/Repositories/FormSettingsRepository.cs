using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for a form's settings. A lost race is conceded, not thrown, the way <c>TenantBrandingRepository</c>
/// concedes one.
/// </summary>
/// <remarks>
/// Two writes made from the same version must not both win. The database decides, not a lock: the
/// <see cref="FormSettings.Version"/> concurrency token makes an update from an older version change no row, and the
/// key refuses a second first row. <see cref="AppDbContext"/> reports either as <see cref="FormSettingsChangedException"/>.
/// </remarks>
internal sealed class FormSettingsRepository(AppDbContext dbContext) : IFormSettingsRepository
{
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
        catch (FormSettingsChangedException)
        {
            // A refused write leaves nothing tracked, so a later save in the same scope does not replay it
            // (ARCHITECTURE.md, transaction locks, item 8).
            DetachRefused(settings);
            return false;
        }
    }

    // The save also wrote the form's webhooks column (FormWebHookSettingsMirror), so the refused change leaves the form
    // too; that part goes with the column (endatix/endatix#1784).
    private void DetachRefused(FormSettings settings)
    {
        dbContext.Entry(settings).State = EntityState.Detached;
        if (dbContext.Forms.Local.FirstOrDefault(form => form.Id == settings.FormId) is { } form)
        {
            dbContext.Entry(form).State = EntityState.Detached;
        }
    }
}
