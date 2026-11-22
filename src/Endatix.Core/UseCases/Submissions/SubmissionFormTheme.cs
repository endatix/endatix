using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Loads the form theme onto a submission's form for submission details, only when the form has a theme and its PDF
/// settings use it. A form without a theme reads no settings; settings that don't use it read no theme.
/// </summary>
internal static class SubmissionFormTheme
{
    public static async Task LoadAsync(
        Submission submission,
        IFormSettingsRepository formSettings,
        IRepository<Theme> themes,
        CancellationToken cancellationToken)
    {
        if (submission.Form is not { ThemeId: { } themeId } form)
        {
            return;
        }

        var settings = await formSettings.ReadAsync(form.TenantId, form.Id, cancellationToken);
        if (!settings.Pdf.UsesFormTheme)
        {
            return;
        }

        if (await themes.GetByIdAsync(themeId, cancellationToken) is { } theme)
        {
            form.SetTheme(theme);
        }
    }
}
