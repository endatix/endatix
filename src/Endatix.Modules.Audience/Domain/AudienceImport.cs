using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One Participant import. A staged import is the state machine of its run: Uploading, Checking, Checked, Writing,
/// then Completed, Canceled, Failed or Expired; only its methods change <see cref="Status"/>, and the status is the
/// row's concurrency token, so two writers that race on a transition settle as compare-and-set. A one-request import
/// is recorded already Completed.
/// </summary>
public sealed class AudienceImport : BaseEntity, IAggregateRoot, ITenantOwned
{
    /// <summary>Database names of the unique indexes on <see cref="AudienceImport"/>.</summary>
    public static class UniqueConstraints
    {
        /// <summary>At most one import per form that can still change.</summary>
        public const string OneUnfinishedPerForm = "IX_Imports_Unfinished";
    }

    /// <summary>Longest author-written failure an import keeps.</summary>
    public const int FailureMaxLength = 500;

    /// <summary>The write checkpoint before any row is written.</summary>
    public const int NothingWritten = -1;

    private AudienceImport()
    {
        FileName = string.Empty;
        Status = string.Empty;
    }

    /// <summary>Records a one-request import that already ran.</summary>
    public AudienceImport(AudienceImportCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NullOrWhiteSpace(args.FileName);

        TenantId = args.TenantId;
        FormId = args.FormId;
        FileName = args.FileName.Trim();
        Status = AudienceImportStatusCodes.Completed;
        CreatedCount = args.CreatedCount;
        UpdatedCount = args.UpdatedCount;
        SkippedCount = args.SkippedCount;
        RejectedCount = args.RejectedCount;
        PerformedById = args.PerformedById;
        CompletedAt = DateTime.UtcNow;
    }

    private AudienceImport(AudienceImportStartArgs args)
    {
        TenantId = args.TenantId;
        FormId = args.FormId;
        FileName = args.FileName.Trim();
        Status = AudienceImportStatusCodes.Uploading;
        HeadersJson = JsonSerializer.Serialize(args.Headers);
        IdentifierColumn = args.IdentifierColumn;
        PropertyColumnsJson = JsonSerializer.Serialize(args.PropertyColumns);
        IsTest = args.IsTest;
        RowCount = args.RowCount;
        ChunkSize = args.ChunkSize;
        PerformedById = args.PerformedById;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    public string FileName { get; private set; }

    public string Status { get; private set; }

    public int CreatedCount { get; private set; }

    public int UpdatedCount { get; private set; }

    public int SkippedCount { get; private set; }

    public int RejectedCount { get; private set; }

    public string? RejectedRowsBlobKey { get; private set; }

    public long? PerformedById { get; private set; }

    /// <summary>When the import reached a status it cannot leave; null while it runs.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>The file's header row, fixed when the import starts; staged cells are positional against it.</summary>
    public string? HeadersJson { get; private set; }

    /// <summary>The header of the column that holds each row's identifier.</summary>
    public string? IdentifierColumn { get; private set; }

    /// <summary>Property variable name → header, fixed when the import starts.</summary>
    public string? PropertyColumnsJson { get; private set; }

    /// <summary>Whether the people this import adds to the form are test participants.</summary>
    public bool IsTest { get; private set; }

    /// <summary>Data rows the file declared when the import started.</summary>
    public int RowCount { get; private set; }

    /// <summary>Rows per staged chunk.</summary>
    public int ChunkSize { get; private set; }

    /// <summary>The match key the Check read rows with. The write refuses another.</summary>
    public string? IdentifierKind { get; private set; }

    /// <summary>Rows the Check has read so far.</summary>
    public int CheckedRowCount { get; private set; }

    /// <summary>Accepted rows whose person is not on the form yet.</summary>
    public int CheckNewCount { get; private set; }

    /// <summary>Accepted rows whose person is already on the form.</summary>
    public int CheckExistingCount { get; private set; }

    /// <summary>Rows the Check rejected.</summary>
    public int CheckRejectedCount { get; private set; }

    /// <summary>
    /// The last spreadsheet row the write has dealt with. It is saved in the same transaction as that batch's
    /// Participants, so a resumed write continues after it and never writes a row twice.
    /// </summary>
    public int WrittenThroughRow { get; private set; } = NothingWritten;

    /// <summary>Accepted rows the write has written so far.</summary>
    public int WrittenRowCount { get; private set; }

    /// <summary>The job running the current phase; null once a phase is committed and before its job is enqueued.</summary>
    public long? JobId { get; private set; }

    /// <summary>Counts the write runs, so a resumed write is enqueued as new work.</summary>
    public int WriteRun { get; private set; }

    /// <summary>Why the import failed, written for the scripter.</summary>
    public string? Failure { get; private set; }

    /// <summary>Whether a failed write may continue from its checkpoint.</summary>
    public bool Resumable { get; private set; }

    /// <summary>Whether the import can still change.</summary>
    public bool IsUnfinished => AudienceImportStatusCodes.Unfinished.Contains(Status);

    /// <summary>The file's header row.</summary>
    public IReadOnlyList<string> Headers =>
        HeadersJson is null ? [] : JsonSerializer.Deserialize<List<string>>(HeadersJson) ?? [];

    /// <summary>Property variable name → header for simple columns.</summary>
    public IReadOnlyDictionary<string, string> PropertyColumns =>
        PropertyColumnsJson is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(PropertyColumnsJson) ?? [];

    /// <summary>Starts a staged import in Uploading.</summary>
    public static AudienceImport Start(AudienceImportStartArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        Guard.Against.NullOrWhiteSpace(args.FileName);
        Guard.Against.NullOrWhiteSpace(args.IdentifierColumn);
        Guard.Against.NegativeOrZero(args.RowCount);
        Guard.Against.NegativeOrZero(args.ChunkSize);
        return new AudienceImport(args);
    }

    /// <summary>The number of chunks the declared rows fill.</summary>
    public int ChunkCount => ChunkSize == 0 ? 0 : (RowCount + ChunkSize - 1) / ChunkSize;

    /// <summary>The last chunk is staged: the Check may start.</summary>
    public void BeginCheck()
    {
        Move(AudienceImportStatusCodes.Checking, AudienceImportStatusCodes.Uploading);
        JobId = null;
    }

    /// <summary>Pins the match key and zeroes the Check's counts, so a retried Check starts at row 1.</summary>
    public void RestartCheck(string identifierKind)
    {
        Ensure(AudienceImportStatusCodes.Checking);
        Guard.Against.NullOrWhiteSpace(identifierKind);
        IdentifierKind = identifierKind;
        CheckedRowCount = 0;
        CheckNewCount = 0;
        CheckExistingCount = 0;
        CheckRejectedCount = 0;
    }

    /// <summary>Adds one chunk's Check counts.</summary>
    public void RecordChecked(CheckedRows rows)
    {
        Ensure(AudienceImportStatusCodes.Checking);
        CheckedRowCount += rows.Read;
        CheckNewCount += rows.New;
        CheckExistingCount += rows.Existing;
        CheckRejectedCount += rows.Rejected;
    }

    /// <summary>The Check has read every row: the scripter may confirm.</summary>
    public void FinishCheck() => Move(AudienceImportStatusCodes.Checked, AudienceImportStatusCodes.Checking);

    /// <summary>The scripter confirms a checked import; the write starts from the first row.</summary>
    public void Confirm()
    {
        Move(AudienceImportStatusCodes.Writing, AudienceImportStatusCodes.Checked);
        RejectedCount = CheckRejectedCount;
        JobId = null;
    }

    /// <summary>Continues a write that stopped, from its checkpoint, as a new write run.</summary>
    public void Resume()
    {
        if (!Resumable)
        {
            throw new DomainRuleException("Only a stopped write can be resumed.");
        }

        Move(AudienceImportStatusCodes.Writing, AudienceImportStatusCodes.Failed);
        Resumable = false;
        Failure = null;
        CompletedAt = null;
        WriteRun++;
        JobId = null;
    }

    /// <summary>Advances the checkpoint and adds one batch's counts, in the same save as its Participants.</summary>
    public void RecordWritten(WrittenRows rows)
    {
        Ensure(AudienceImportStatusCodes.Writing);
        Guard.Against.OutOfRange(rows.ThroughRow, nameof(rows.ThroughRow), WrittenThroughRow + 1, int.MaxValue);
        WrittenThroughRow = rows.ThroughRow;
        WrittenRowCount += rows.Written;
        CreatedCount += rows.Created;
        UpdatedCount += rows.Updated;
        SkippedCount += rows.Skipped;
        RejectedCount += rows.Rejected;
    }

    /// <summary>The write has written every row.</summary>
    public void Complete()
    {
        Move(AudienceImportStatusCodes.Completed, AudienceImportStatusCodes.Writing);
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Stops the import before anything is written.</summary>
    public void Cancel()
    {
        Move(
            AudienceImportStatusCodes.Canceled,
            AudienceImportStatusCodes.Uploading,
            AudienceImportStatusCodes.Checking,
            AudienceImportStatusCodes.Checked);
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Ends an import left uploading or checked for too long.</summary>
    public void Expire()
    {
        Move(AudienceImportStatusCodes.Expired, AudienceImportStatusCodes.Uploading, AudienceImportStatusCodes.Checked);
        CompletedAt = DateTime.UtcNow;
    }

    /// <param name="reason">Author-written text the scripter reads; never exception text.</param>
    /// <param name="resumable">Whether a stopped write may continue from its checkpoint.</param>
    public void Fail(string reason, bool resumable = false)
    {
        Guard.Against.NullOrWhiteSpace(reason);
        bool wasWriting = Status == AudienceImportStatusCodes.Writing;
        Move(AudienceImportStatusCodes.Failed, AudienceImportStatusCodes.Checking, AudienceImportStatusCodes.Writing);
        Failure = reason.Length > FailureMaxLength ? reason[..FailureMaxLength] : reason;
        Resumable = resumable && wasWriting;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Links the import to the background job running its current phase.</summary>
    public void RecordJob(long jobId)
    {
        Guard.Against.NegativeOrZero(jobId);
        JobId = jobId;
    }

    private void Move(string to, params string[] from)
    {
        if (!from.Contains(Status))
        {
            throw new DomainRuleException($"An import that is {Status} cannot become {to}.");
        }

        Status = to;
    }

    private void Ensure(string status)
    {
        if (Status != status)
        {
            throw new DomainRuleException($"The import is {Status}, not {status}.");
        }
    }
}

/// <summary>
/// Construction args for a one-request <see cref="AudienceImport"/>.
/// </summary>
public sealed record AudienceImportCreateArgs(
    long TenantId,
    long FormId,
    string FileName,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int RejectedCount,
    long? PerformedById);

/// <summary>
/// What a staged <see cref="AudienceImport"/> is fixed to when it starts.
/// </summary>
public sealed record AudienceImportStartArgs(
    long TenantId,
    long FormId,
    string FileName,
    IReadOnlyList<string> Headers,
    string IdentifierColumn,
    IReadOnlyDictionary<string, string> PropertyColumns,
    bool IsTest,
    int RowCount,
    int ChunkSize,
    long? PerformedById);

/// <summary>One chunk's Check counts.</summary>
public sealed record CheckedRows(int Read, int New, int Existing, int Rejected);

/// <summary>One write batch: the last row it dealt with, and what it wrote.</summary>
public sealed record WrittenRows(int ThroughRow, int Written, int Created, int Updated, int Skipped, int Rejected);

/// <summary>
/// Wire codes for <see cref="AudienceImport.Status"/>.
/// </summary>
public static class AudienceImportStatusCodes
{
    /// <summary>The browser is sending the file's chunks.</summary>
    public const string Uploading = "uploading";
    /// <summary>The Check is reading every row; nothing is written.</summary>
    public const string Checking = "checking";
    /// <summary>The Check is done; the import waits for the scripter to confirm.</summary>
    public const string Checked = "checked";
    /// <summary>The write is adding and updating Participants.</summary>
    public const string Writing = "writing";
    public const string Completed = "completed";
    /// <summary>Canceled before anything was written.</summary>
    public const string Canceled = "canceled";
    /// <summary>Stopped with a reason; a stopped write may be resumable.</summary>
    public const string Failed = "failed";
    /// <summary>Left uploading or checked for too long.</summary>
    public const string Expired = "expired";

    /// <summary>The one list of statuses an import can still leave: the unfinished-import index derives from it.</summary>
    public static readonly IReadOnlyList<string> Unfinished = [Uploading, Checking, Checked, Writing];
}
