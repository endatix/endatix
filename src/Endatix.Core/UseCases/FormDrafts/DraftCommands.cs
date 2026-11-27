using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDrafts;

/// <summary>The editable survey, its current precondition, and how it relates to the live form.
/// RecordsSinceSnapshot is the number of journal emissions after the last snapshot through Version;
/// it is zero before the first write and immediately after a snapshot.</summary>
[Experimental("ENDATIX0007")]
public sealed record DraftResponse(long Version, string JsonData, string ContentHash, int? ActiveNumber,
    bool HasUnpublishedChanges, DateTime? LastModifiedAt, Actor? LastModifiedBy, int RecordsSinceSnapshot);

/// <summary>Read the draft or the unsaved projection of the active survey.</summary>
[Experimental("ENDATIX0007")]
public sealed record ReadDraft(long FormId) : IQuery<Result<DraftResponse>>;

/// <summary>A conditional edit to a form's draft.</summary>
[Experimental("ENDATIX0007")]
public abstract record WriteDraft(long FormId, long ExpectedVersion) : ICommand<Result<DraftResponse>>;
/// <summary>Replace the whole survey with one snapshot.</summary>
[Experimental("ENDATIX0007")]
public sealed record ReplaceDraft(long FormId, long ExpectedVersion, string JsonData) : WriteDraft(FormId, ExpectedVersion);
/// <summary>Append a batch of journal records and advance the draft head.</summary>
[Experimental("ENDATIX0007")]
public sealed record AppendDraft(long FormId, long ExpectedVersion, DraftJournalBatch Batch) : WriteDraft(FormId, ExpectedVersion);
/// <summary>Append a snapshot of the active survey without removing the edit history.</summary>
[Experimental("ENDATIX0007")]
public sealed record DiscardDraft(long FormId, long ExpectedVersion) : WriteDraft(FormId, ExpectedVersion);
