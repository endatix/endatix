using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>One immutable step in a form draft's history.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class FormDraftOperation : ITenantOwned, IInsertOnly
{
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
    public string? Label { get; internal init; }
    /// <summary>Optional author note for a named snapshot.</summary>
    public string? Note { get; internal init; }
    /// <summary>The historical version selected for a restore or repair.</summary>
    public long? TargetVersion { get; internal init; }
    /// <summary>The principal or system responsible for the write.</summary>
    public Actor Actor { get; internal init; } = null!;
    /// <summary>Writer channel assigned by the server.</summary>
    public string Channel { get; internal init; } = null!;
    /// <summary>Client-supplied grouping label for an editing session.</summary>
    public string? ClientSessionId { get; internal init; }
    /// <summary>UTC time of the batch or snapshot.</summary>
    public DateTime CreatedAt { get; internal init; }
}

internal sealed record DraftWrite(Actor Actor, string Channel, DateTime At)
{
    internal static DraftWrite System(DateTime at) => new(Actor.System, "system", at);
}

/// <summary>One autosave: ordered journal emissions and the complete resulting survey.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed record DraftJournalBatch(int SaveNo, string ClientSessionId, IReadOnlyList<string> Records, string JsonData, string ContentHash)
{
    /// <summary>Interior snapshots in ascending order. The terminal survey is already in JsonData.</summary>
    public IReadOnlyList<DraftCheckpoint> Checkpoints { get; init; } = [];
}

/// <summary>The survey as it stood after AfterRecord emissions in this batch (one-based, not the client seq).</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed record DraftCheckpoint(int AfterRecord, string JsonData);
