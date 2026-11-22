using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormSettings;

namespace Endatix.Api.Endpoints.FormSettings;

/// <summary>A form's settings on the wire: every section, and the version a write names in <c>If-Match</c>.</summary>
[Experimental("ENDATIX0005")]
public sealed class FormSettingsModel
{
    /// <summary>The version to write from; 0 before the first write. The <c>ETag</c> header holds the same value.</summary>
    public long Version { get; init; }

    /// <summary>How the form's submission PDFs look.</summary>
    public PdfSettingsModel Pdf { get; init; } = new();

    internal static FormSettingsModel Map(FormSettingsDto settings) => new()
    {
        Version = settings.Version,
        Pdf = PdfSettingsModel.Map(settings.Pdf),
    };
}

/// <summary>The PDF section on the wire.</summary>
[Experimental("ENDATIX0005")]
public sealed class PdfSettingsModel
{
    /// <summary>Whether the form's submission PDFs use its theme. Off by default.</summary>
    public bool UsesFormTheme { get; init; }

    internal static PdfSettingsModel Map(PdfSettings pdf) => new() { UsesFormTheme = pdf.UsesFormTheme };

    internal PdfSettings ToSection() => new() { UsesFormTheme = UsesFormTheme };
}
