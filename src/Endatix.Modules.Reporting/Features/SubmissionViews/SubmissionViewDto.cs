using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Features.SubmissionViews;

/// <summary>A form's submissions grid view as the grid reads it.</summary>
/// <param name="Version">The version to send back with the next save; <c>0</c> when nobody saved a view.</param>
/// <param name="SettingsJson">The settings, in the shape <see cref="SubmissionViewSettings"/> describes.</param>
public sealed record SubmissionViewDto(long Version, string SettingsJson)
{
    /// <summary>The view of a form nobody saved one for.</summary>
    public static readonly SubmissionViewDto Default =
        new(SubmissionView.UnsavedVersion, SubmissionViewSettings.DefaultJson);
}
