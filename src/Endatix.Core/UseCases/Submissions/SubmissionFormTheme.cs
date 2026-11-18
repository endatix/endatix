using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Loads the form theme onto a submission's form for submission details, only when the form's PDFs use it.
/// Otherwise the theme JSON is never read.
/// </summary>
internal static class SubmissionFormTheme
{
    public static async Task LoadAsync(Submission submission, IRepository<Theme> themes, CancellationToken cancellationToken)
    {
        if (submission.Form is not { PdfUsesFormTheme: true, ThemeId: { } themeId })
        {
            return;
        }

        var theme = await themes.GetByIdAsync(themeId, cancellationToken);
        if (theme is not null)
        {
            submission.Form.SetTheme(theme);
        }
    }
}
