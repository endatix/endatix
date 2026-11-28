using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Entities;

/// <summary>
/// A form's per-feature settings, one row per form. Each feature owns one JSON section, read only by that feature, so
/// loading a form never loads its settings. A missing row or section means the section's defaults.
/// </summary>
/// <remarks>
/// <see cref="Version"/> is the optimistic concurrency token: a write names the version it read and is refused when
/// another write came first. A write whose sections already hold the stored values changes nothing, whatever version it
/// names, so a retried request is a no-op.
/// </remarks>
[Experimental("ENDATIX0005")]
public sealed class FormSettings : IAggregateRoot, ITenantOwned, IHasVersion, IHasDomainModificationTime
{
    /// <summary>The <see cref="Version"/> a write names when the form has no settings row yet.</summary>
    public const long UNSAVED_VERSION = 0;

    private FormSettings() { } // For EF Core

    private FormSettings(long tenantId, long formId)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        TenantId = tenantId;
        FormId = formId;
    }

    /// <summary>Starts <paramref name="form"/>'s settings with every section at its defaults, at
    /// <see cref="UNSAVED_VERSION"/>, in the form's tenant. Settings exist only for a form, so a form is the only way to
    /// start them.</summary>
    public static FormSettings For(Form form)
    {
        Guard.Against.Null(form);
        return new FormSettings(form.TenantId, form.Id);
    }

    /// <summary>The form, and the primary key.</summary>
    public long FormId { get; private set; }

    /// <inheritdoc/>
    public long TenantId { get; private set; }

    /// <summary>Goes up by one on every change. 1 after the first write; <see cref="UNSAVED_VERSION"/> before it.</summary>
    public long Version { get; private set; } = UNSAVED_VERSION;

    /// <summary>When a section last changed; null before the first write.</summary>
    public DateTime? ModifiedAt { get; private set; }

    /// <summary>The PDF section as stored; null means its defaults.</summary>
    public string? PdfJson { get; private set; }

    /// <summary>The PDF section, with defaults for anything not stored.</summary>
    [NotMapped]
    public PdfSettings Pdf => FormSettingsJson.Read<PdfSettings>(PdfJson);

    /// <summary>The sections whose stored JSON cannot be read, so they read as their defaults; named as on the wire.</summary>
    internal IEnumerable<string> UnreadableSections()
    {
        if (!FormSettingsJson.IsReadable<PdfSettings>(PdfJson))
        {
            yield return "pdf";
        }
    }

    /// <summary>Replaces each section <paramref name="change"/> names whose value differs from the stored one.</summary>
    /// <param name="change">The sections to replace, each whole; a null section is left alone.</param>
    /// <param name="expectedVersion">The <see cref="Version"/> the caller read.</param>
    /// <param name="at">When the change is made.</param>
    /// <returns>True when a section changed (the version goes up); false when every named section already holds its
    /// value, whatever <paramref name="expectedVersion"/> is; <see cref="ResultStatus.Conflict"/> when a section differs
    /// and <paramref name="expectedVersion"/> is stale.</returns>
    /// <remarks>It compares with the stored JSON, not the resolved section, so a value equal to today's default is still
    /// recorded when it was never stored.</remarks>
    internal Result<bool> Apply(FormSettingsChange change, long expectedVersion, DateTime at)
    {
        Guard.Against.Null(change);
        var pdfJson = Changed(change.Pdf, PdfJson);
        if (pdfJson is null)
        {
            return false;
        }

        if (expectedVersion != Version)
        {
            return Result.Conflict($"The form settings changed since they were read; they are now at version {Version}.");
        }

        PdfJson = pdfJson;
        ModifiedAt = at;
        Version++;
        return true;
    }

    // The new canonical JSON when the section is named and differs from what is stored; otherwise null.
    private static string? Changed<T>(T? section, string? stored) where T : class
    {
        if (section is null)
        {
            return null;
        }

        var json = FormSettingsJson.Write(section);
        return FormSettingsJson.SameContent(json, stored) ? null : json;
    }
}

/// <summary>The sections a form settings write replaces; a null section is left as it is.</summary>
[Experimental("ENDATIX0005")]
public sealed record FormSettingsChange
{
    /// <summary>The PDF section, replaced whole.</summary>
    public PdfSettings? Pdf { get; init; }
}

/// <summary>How a form's submission PDFs look. The C# defaults are the product defaults.</summary>
[Experimental("ENDATIX0005")]
public sealed record PdfSettings
{
    /// <summary>Whether the form's submission PDFs use its theme. Off by default.</summary>
    public bool UsesFormTheme { get; init; }
}
