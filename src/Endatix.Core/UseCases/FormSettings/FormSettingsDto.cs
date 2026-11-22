using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.FormSettings;

/// <summary>A form's settings: every section, with defaults where nothing is stored, and the version to write from.</summary>
/// <param name="Version">The version a write names; <see cref="Entities.FormSettings.UNSAVED_VERSION"/> before the first write.</param>
/// <param name="Pdf">The PDF section.</param>
/// <param name="WebHooks">The form's own webhook settings; null when its tenant's apply.</param>
[Experimental("ENDATIX0005")]
public sealed record FormSettingsDto(long Version, PdfSettings Pdf, WebHookConfiguration? WebHooks)
{
    /// <summary><paramref name="form"/>'s settings as they are now, with the webhooks it sends with
    /// (<see cref="Entities.FormSettings.WebHooksOf"/>).</summary>
    internal static FormSettingsDto From(Entities.FormSettings settings, Form form) =>
        new(settings.Version, settings.Pdf, settings.WebHooksOf(form));
}
