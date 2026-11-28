using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.Entities;

/// <summary>The current editable survey, separate from the definition shown to respondents.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class FormDraft : IAggregateRoot, ITenantOwned, IHasVersion, IHasDomainModificationTime
{
    /// <summary>The form whose draft this is.</summary>
    public long FormId { get; private init; }
    /// <inheritdoc/>
    public long TenantId { get; private init; }
    /// <summary>Survey text as saved by the editor.</summary>
    public string JsonData { get; private set; } = null!;
    /// <summary>Server-computed SHA-256 of the canonical survey JSON.</summary>
    public string ContentHash { get; private set; } = null!;
    /// <inheritdoc/>
    public long Version { get; private set; }
    /// <summary>UTC time of the latest save; null for an unpersisted draft.</summary>
    public DateTime? ModifiedAt { get; private set; }

    internal static FormDraft For(Form form, string publishedJson) => new()
    {
        FormId = form.Id,
        TenantId = form.TenantId,
        JsonData = publishedJson,
        ContentHash = CanonicalSurveyJson.PublishedHash(publishedJson)
    };

    /// <summary>Preserves both the previous live document and the adopted legacy edit in replay order.</summary>
    public static FormDraft Adopt(Form form, FormDefinition stray, ICollection<FormDraftOperation> operations)
    {
        if (stray.FormId != form.Id || stray.TenantId != form.TenantId || !stray.IsDraft)
        {
            throw new InvalidOperationException("A stray draft must belong to the form being upgraded.");
        }
        var draft = For(form, form.ActiveDefinition?.JsonData ?? FormDefinitionDefaults.Json);
        var write = DraftWrite.System(stray.ModifiedAt ?? stray.CreatedAt);
        foreach (var row in draft.WriteSnapshot(stray.JsonData, stray.ContentHash, ("adopt", write)))
        {
            operations.Add(row);
        }
        return draft;
    }

    internal List<FormDraftOperation> Replace(string json, DraftWrite write)
    {
        var hash = CanonicalSurveyJson.Hash(json);
        return hash == ContentHash ? [] : WriteSnapshot(json, hash, (DraftSnapshotReasons.WHOLE_JSON, write));
    }

    internal List<FormDraftOperation> Discard(string json, DraftWrite write) =>
        WriteSnapshot(json, CanonicalSurveyJson.PublishedHash(json), (DraftSnapshotReasons.DISCARD, write));

    internal Result<List<FormDraftOperation>> Append(DraftJournalBatch batch, DraftWrite write, long latestSnapshotVersion)
    {
        if (batch.ValidationError() is { } error)
        {
            return Result.Invalid(error);
        }
        var prepared = DraftBatchCheckpoints.Prepare(batch, Version - latestSnapshotVersion);
        if (!prepared.IsSuccess)
        {
            return prepared.ToErrorResult<List<FormDraftOperation>>();
        }
        return AppendPrepared(batch, prepared.Value, write);
    }

    private List<FormDraftOperation> AppendPrepared(DraftJournalBatch batch, DraftBatchCheckpoints plan, DraftWrite write)
    {
        var rows = SeedIfNeeded(write.At);
        if (plan.SnapshotHead)
        {
            rows.Add(Snapshot(DraftSnapshotReasons.PERIODIC, DraftWrite.System(write.At)));
        }
        for (var index = 0; index < batch.Records.Count; index++)
        {
            rows.Add(JournalRow(batch, index, write));
            if (plan.Documents.TryGetValue(index + 1, out var document))
            {
                SetContent(document);
                rows.Add(Snapshot(DraftSnapshotReasons.PERIODIC, write));
            }
        }
        SetContent(plan.Terminal);
        ModifiedAt = write.At;
        return rows;
    }

    private void SetContent(DraftDocument document)
    {
        JsonData = document.Json;
        ContentHash = document.Hash;
    }

    private FormDraftOperation JournalRow(DraftJournalBatch batch, int index, DraftWrite write) => new()
    {
        FormId = FormId,
        TenantId = TenantId,
        Version = ++Version,
        Kind = FormDraftOperationKinds.JOURNAL,
        Payload = batch.Records[index],
        EditorSessionId = batch.ClientSessionId,
        ContentHash = index == batch.Records.Count - 1 ? batch.ContentHash : null,
        Actor = write.Actor with { },
        Channel = write.Channel,
        CreatedAt = write.At
    };

    private List<FormDraftOperation> WriteSnapshot(string json, string hash, (string Reason, DraftWrite Stamp) write)
    {
        var rows = SeedIfNeeded(write.Stamp.At);
        JsonData = json;
        ContentHash = hash;
        rows.Add(Snapshot(write.Reason, write.Stamp));
        return rows;
    }

    private List<FormDraftOperation> SeedIfNeeded(DateTime at) =>
        Version == 0 ? [Snapshot(DraftSnapshotReasons.SEED, DraftWrite.System(at))] : [];

    private FormDraftOperation Snapshot(string reason, DraftWrite write)
    {
        ModifiedAt = write.At;
        return new()
        {
            FormId = FormId,
            TenantId = TenantId,
            Version = ++Version,
            Kind = FormDraftOperationKinds.SNAPSHOT,
            Reason = reason,
            Payload = JsonData,
            ContentHash = ContentHash,
            Actor = write.Actor with { },
            Channel = write.Channel,
            CreatedAt = write.At
        };
    }
}
