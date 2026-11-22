using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Loads the form theme onto a submission's form for submission details, only when the form has a theme and its PDF
/// settings use it. A form without a theme reads no settings; settings that don't use it read no theme. A stored section
/// that cannot be read is logged and reads as its defaults.
/// </summary>
[Experimental("ENDATIX0005")]
public sealed partial class SubmissionFormTheme(
    IFormSettingsRepository formSettings,
    IRepository<Theme> themes,
    ILogger<SubmissionFormTheme> logger)
{
    /// <summary>Sets the theme on <paramref name="submission"/>'s form when its PDF settings use it.</summary>
    public async Task LoadAsync(Submission submission, CancellationToken cancellationToken)
    {
        if (submission.Form is not { ThemeId: { } themeId } form)
        {
            return;
        }

        var settings = await formSettings.ReadAsync(form.TenantId, form.Id, cancellationToken);
        foreach (var section in settings.UnreadableSections())
        {
            LogUnreadableSection(form.Id, section);
        }

        if (settings.Pdf.UsesFormTheme && await themes.GetByIdAsync(themeId, cancellationToken) is { } theme)
        {
            form.SetTheme(theme);
        }
    }

    [LoggerMessage(
        EventName = "FormSettingsSectionUnreadable",
        Level = LogLevel.Warning,
        Message = "Form {FormId} has a stored {Section} settings section that cannot be read; using its defaults until the next write replaces it.")]
    private partial void LogUnreadableSection(long formId, string section);
}
