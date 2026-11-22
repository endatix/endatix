using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Keeps a form's two copies of its webhook settings the same within one save: <c>Forms.WebHookSettingsJson</c>, which
/// the previous release reads, and the <see cref="FormSettings.WebHooksJson"/> section, which this release reads. A
/// write to either is copied to the other before the save, so both go in one transaction whichever endpoint wrote them,
/// and a rolling deploy or a rollback loses nothing. Delete it with the column (endatix/endatix#1784).
/// </summary>
/// <remarks>
/// A form whose column changed copies the column into its section, as stored (the copy the migration made). A settings
/// write writes the form it was given itself (<c>FormSettings.Apply</c>), so its section and column arrive here
/// equal; a section changed while its form is not tracked in this save is written to the stored form through
/// <see cref="Form.UpdateWebHookSettings"/>, in the shape the previous release reads. A form changed in the same save
/// wins over its section. Either copy raises the settings' <see cref="FormSettings.Version"/>, so a form save can lose
/// to another write of the same form's settings; <see cref="AppDbContext"/> reports that as
/// <c>FormSettingsChangedException</c>. A write this cannot see (an older
/// instance's, or a raw update) leaves the two apart, and readers then take the column
/// (<see cref="FormSettings.WebHooksOf"/>).
/// </remarks>
internal static class FormWebHookSettingsMirror
{
    /// <summary>Copies each changed side to the other in <paramref name="db"/>'s tracked changes.</summary>
    public static void Apply(AppDbContext db, DateTime at)
    {
        var (forms, sections) = Changes(db);
        foreach (var form in forms)
        {
            var stored = NeedsStoredSettings(db, form) ? StoredSettings(db, form.Id).FirstOrDefault() : null;
            SettingsOf(db, form, stored)?.CopyWebHooksFrom(form, at);
        }

        foreach (var settings in sections)
        {
            var form = TrackedForm(db, settings.FormId) ?? StoredForm(db, settings.FormId).FirstOrDefault();
            form?.UpdateWebHookSettings(settings.WebHooks);
        }
    }

    /// <inheritdoc cref="Apply"/>
    public static async Task ApplyAsync(AppDbContext db, DateTime at, CancellationToken cancellationToken)
    {
        var (forms, sections) = Changes(db);
        foreach (var form in forms)
        {
            var stored = NeedsStoredSettings(db, form)
                ? await StoredSettings(db, form.Id).FirstOrDefaultAsync(cancellationToken)
                : null;
            SettingsOf(db, form, stored)?.CopyWebHooksFrom(form, at);
        }

        foreach (var settings in sections)
        {
            var form = TrackedForm(db, settings.FormId)
                ?? await StoredForm(db, settings.FormId).FirstOrDefaultAsync(cancellationToken);
            form?.UpdateWebHookSettings(settings.WebHooks);
        }
    }

    // Forms whose column changed, and sections a settings write changed on a form not changed in the same save.
    private static (List<Form> Forms, List<FormSettings> Sections) Changes(AppDbContext db)
    {
        var forms = ChangedForms(db);
        var sections = ChangedSections(db).Where(settings => forms.TrueForAll(form => form.Id != settings.FormId)).ToList();
        return (forms, sections);
    }

    // New forms with webhook settings, and saved forms whose webhook settings changed. A whole-form update marks the
    // column modified when it did not change, so the value is compared. A change this misses (a detached form attached
    // by Update) leaves the section behind, and the column wins (FormSettings.WebHooksOf).
    private static List<Form> ChangedForms(AppDbContext db) =>
        [.. db.ChangeTracker.Entries<Form>()
            .Where(entry => entry.State switch
            {
                EntityState.Added => !string.IsNullOrWhiteSpace(entry.Entity.WebHookSettingsJson),
                EntityState.Modified => entry.Property(form => form.WebHookSettingsJson) is { IsModified: true } column
                    && !string.Equals(column.OriginalValue, column.CurrentValue, StringComparison.Ordinal),
                _ => false,
            })
            .Select(entry => entry.Entity)];

    // Sections a settings write stored. A new row with no webhooks section leaves the form's column as it is.
    private static List<FormSettings> ChangedSections(AppDbContext db) =>
        [.. db.ChangeTracker.Entries<FormSettings>()
            .Where(entry => entry.State switch
            {
                EntityState.Added => entry.Entity.WebHooksJson is not null,
                EntityState.Modified => entry.Property(settings => settings.WebHooksJson).IsModified,
                _ => false,
            })
            .Select(entry => entry.Entity)];

    // A new form has no stored row yet, and a tracked row is already at hand.
    private static bool NeedsStoredSettings(AppDbContext db, Form form) =>
        db.Entry(form).State != EntityState.Added && TrackedSettings(db, form.Id) is null;

    // The form's settings: tracked, stored, or new (added to the save) when the form has webhooks to copy. A whole-form
    // update marks the column modified even when it did not change, so a form without webhooks gets no row.
    private static FormSettings? SettingsOf(AppDbContext db, Form form, FormSettings? stored)
    {
        var settings = TrackedSettings(db, form.Id) ?? stored;
        if (settings is null && !string.IsNullOrWhiteSpace(form.WebHookSettingsJson))
        {
            settings = FormSettings.For(form);
            db.FormSettings.Add(settings);
        }

        return settings;
    }

    private static FormSettings? TrackedSettings(AppDbContext db, long formId) =>
        db.FormSettings.Local.FirstOrDefault(candidate => candidate.FormId == formId);

    // By key, so no tenant filter is needed: the form it belongs to is already in this save.
    private static IQueryable<FormSettings> StoredSettings(AppDbContext db, long formId) =>
        db.FormSettings.IgnoreQueryFilters().Where(candidate => candidate.FormId == formId);

    private static Form? TrackedForm(AppDbContext db, long formId) =>
        db.Forms.Local.FirstOrDefault(candidate => candidate.Id == formId);

    // A deleted form keeps its settings row, so it is found too.
    private static IQueryable<Form> StoredForm(AppDbContext db, long formId) =>
        db.Forms.IgnoreQueryFilters().Where(candidate => candidate.Id == formId);
}
