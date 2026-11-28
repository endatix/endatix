using Endatix.Core.Abstractions;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

/// <summary>One immutable step in a form draft's history.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class FormDraftOperation : ITenantOwned, IInsertOnly
{
    /// <summary>Room for a UUID or an editor-prefixed session token. Opaque labels need not be UUIDs.</summary>
    public const int EDITOR_SESSION_ID_MAX_LENGTH = 128;
    /// <summary>A named snapshot has a display title, like a data list item label.</summary>
    public const int LABEL_MAX_LENGTH = 100;

    /// <summary>The owning form.</summary>
    public long FormId { get; internal init; }
    /// <summary>Server-assigned position within this draft.</summary>
    public long Version { get; internal init; }
    /// <inheritdoc/>
    public long TenantId { get; internal init; }
    /// <summary>Either a journal record or a full snapshot.</summary>
    public string Kind { get; internal init; } = null!;
    /// <summary>The emitted journal record or the complete survey JSON.</summary>
    public string Payload { get; internal init; } = null!;
    /// <summary>Server hash on snapshots; the writer's hash on the final record of a batch.</summary>
    public string? ContentHash { get; internal init; }
    /// <summary>Why the snapshot was taken; null for an edit.</summary>
    public string? Reason { get; internal init; }
    /// <summary>Title of a named snapshot.</summary>
    public string? Label
    {
        get;
        internal init
        {
            DomainValidationException.ThrowIfError(LabelError(value), nameof(Label));
            field = value;
        }
    }

    /// <summary>Checks the optional display title before it reaches either provider.</summary>
    public static string? LabelError(string? label) =>
        label is not null && (label.Length > LABEL_MAX_LENGTH || label.Contains('\0') || !DraftJsonEncoding.IsValidUnicode(label))
            ? $"A snapshot title can contain up to {LABEL_MAX_LENGTH} UTF-16 code units with valid Unicode and no NUL."
            : null;
    /// <summary>Optional free-form changelog text for a named snapshot; deliberately unbounded.</summary>
    public string? Note { get; internal init; }
    /// <summary>The historical version selected for a restore or repair.</summary>
    public long? TargetVersion { get; internal init; }
    /// <summary>The principal or system responsible for the write.</summary>
    public Actor Actor { get; internal init; } = null!;
    /// <summary>Writer channel assigned by the server.</summary>
    public string Channel { get; internal init; } = null!;
    /// <summary>Client-supplied grouping label for an editing session.</summary>
    public string? EditorSessionId { get; internal init; }
    /// <summary>UTC time of the batch or snapshot.</summary>
    public DateTime CreatedAt { get; internal init; }
}

internal sealed record DraftWrite(Actor Actor, string Channel, DateTime At)
{
    internal static DraftWrite System(DateTime at) => new(Actor.System, DraftChannels.SYSTEM, at);
}

/// <summary>One autosave: ordered journal emissions and the complete resulting survey.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed record DraftJournalBatch(int SaveNo, string ClientSessionId, IReadOnlyList<string> Records, string JsonData, string ContentHash)
{
    /// <summary>Validates the self-reported session label in UTF-16 code units on either provider.</summary>
    public static string? EditorSessionIdError(string? sessionId) =>
        string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > FormDraftOperation.EDITOR_SESSION_ID_MAX_LENGTH ||
        sessionId.Contains('\0') || !DraftJsonEncoding.IsValidUnicode(sessionId)
            ? $"Provide an editing session identifier of up to {FormDraftOperation.EDITOR_SESSION_ID_MAX_LENGTH} UTF-16 code units with valid Unicode and no NUL."
            : null;

    private static bool IsContentHash(string? hash) =>
        hash is { Length: CanonicalContentHash.LENGTH } && hash.All(char.IsAsciiHexDigit);

    internal ValidationError? ValidationError()
    {
        if (EditorSessionIdError(ClientSessionId) is { } error)
        {
            return new(error) { Identifier = "clientSessionId" };
        }
        if (!IsContentHash(ContentHash))
        {
            return new("Provide a SHA-256 hash in hexadecimal.") { Identifier = "contentHash" };
        }
        return Records.All(DraftJsonEncoding.HasValidStrings) ? null
            : new("Journal records must contain valid Unicode.") { Identifier = "records" };
    }

    /// <summary>Interior snapshots in ascending order. The terminal survey is already in JsonData.</summary>
    public IReadOnlyList<DraftCheckpoint> Checkpoints { get; init; } = [];
}

/// <summary>The survey as it stood after AfterRecord emissions in this batch (one-based, not the client seq).</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed record DraftCheckpoint(int AfterRecord, string JsonData);
