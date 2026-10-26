using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Endpoints.Audience.Import;
using Endatix.Modules.Audience.Features.Imports;

namespace Endatix.Modules.Audience.Endpoints.Audience.Imports;

/// <summary>
/// Wire model for one staged import: its status, how far each phase got, and the first rejected rows.
/// </summary>
public sealed class AudienceImportResponse
{
    /// <summary>The import's id.</summary>
    public long ImportId { get; init; }

    /// <summary>uploading, checking, checked, writing, completed, canceled, failed or expired.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The file name the scripter chose.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// The file's header row, fixed at start; staged cells are positional against it, so a file chosen again to resume
    /// an upload must have the same headers.
    /// </summary>
    public IReadOnlyList<string> Headers { get; init; } = [];

    /// <summary>Whether new people become test participants.</summary>
    public bool IsTest { get; init; }

    /// <summary><c>keep</c> or <c>clear</c>: what an empty cell does to a stored value.</summary>
    public string EmptyCells { get; init; } = AudienceImportOptionCodes.Keep;

    /// <summary><c>keep</c> or <c>clear</c>: what the write does to the Properties the file has no column for.</summary>
    public string MissingProperties { get; init; } = AudienceImportOptionCodes.Keep;

    /// <summary>The file's size, and the chunks still to send while uploading.</summary>
    public AudienceImportUploadResponse Upload { get; init; } = new();

    /// <summary>Whether the import runs in the background, so the scripter may close the page.</summary>
    public bool RunsInBackground { get; init; }

    /// <summary>Rows the Check has read so far.</summary>
    public int CheckedRowCount { get; init; }

    /// <summary>The Check's counts, a preview; null until the Check has read every row.</summary>
    public AudienceImportCheckResponse? Check { get; init; }

    /// <summary>What the write has written so far.</summary>
    public AudienceImportWrittenResponse Written { get; init; } = new();

    /// <summary>The first 50 rejected rows, in row order.</summary>
    public IReadOnlyList<AudienceImportRejectionResponse> Rejections { get; init; } = [];

    /// <summary>Why the import failed, for the scripter.</summary>
    public string? Failure { get; init; }

    /// <summary>Whether a stopped write can be resumed with status writing.</summary>
    public bool CanResume { get; init; }

    /// <summary>The background job of the current phase, for operators.</summary>
    public long? JobId { get; init; }

    /// <summary>When the import started.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the import reached a status it cannot leave.</summary>
    public DateTime? FinishedAt { get; init; }

    internal static AudienceImportResponse FromDto(ImportViewDto dto) => new()
    {
        ImportId = dto.ImportId,
        Status = dto.Status,
        FileName = dto.FileName,
        Headers = dto.Headers,
        IsTest = dto.IsTest,
        EmptyCells = AudienceImportOptionCodes.Of(dto.Options.ClearEmptyCells),
        MissingProperties = AudienceImportOptionCodes.Of(dto.Options.ClearMissingProperties),
        Upload = AudienceImportUploadResponse.FromDto(dto.Upload),
        RunsInBackground = dto.Outcome.RunsInBackground,
        CheckedRowCount = dto.CheckedRowCount,
        Check = dto.Check is { } check ? AudienceImportCheckResponse.FromDto(check) : null,
        Written = AudienceImportWrittenResponse.FromDto(dto.Written),
        Rejections = [.. dto.Rejections.Select(AudienceImportRejectionResponse.FromDto)],
        Failure = dto.Outcome.Failure,
        CanResume = dto.Outcome.CanResume,
        JobId = dto.Outcome.JobId,
        CreatedAt = dto.Outcome.CreatedAt,
        FinishedAt = dto.Outcome.FinishedAt,
    };
}

/// <summary>The file's size, and the chunks still to send while uploading.</summary>
public sealed class AudienceImportUploadResponse
{
    /// <summary>Data rows the file declared.</summary>
    public int RowCount { get; init; }

    /// <summary>Rows per chunk the upload sends.</summary>
    public int ChunkSize { get; init; }

    /// <summary>The chunks still to send; empty once the upload is complete.</summary>
    public IReadOnlyList<int> MissingChunks { get; init; } = [];

    internal static AudienceImportUploadResponse FromDto(ImportUploadDto dto) => new()
    {
        RowCount = dto.RowCount,
        ChunkSize = dto.ChunkSize,
        MissingChunks = dto.MissingChunks,
    };
}

/// <summary>The Check's counts.</summary>
public sealed class AudienceImportCheckResponse
{
    /// <summary>Accepted rows whose person is not on the form yet.</summary>
    public int NewCount { get; init; }

    /// <summary>Accepted rows whose person is already on the form.</summary>
    public int ExistingCount { get; init; }

    /// <summary>Rows the Check rejected.</summary>
    public int RejectedCount { get; init; }

    internal static AudienceImportCheckResponse FromDto(ImportCheckDto dto) => new()
    {
        NewCount = dto.NewCount,
        ExistingCount = dto.ExistingCount,
        RejectedCount = dto.RejectedCount,
    };
}

/// <summary>What the write has written so far.</summary>
public sealed class AudienceImportWrittenResponse
{
    /// <summary>Accepted rows written so far.</summary>
    public int WrittenRowCount { get; init; }

    /// <summary>People added to the form.</summary>
    public int CreatedCount { get; init; }

    /// <summary>People on the form whose values changed.</summary>
    public int UpdatedCount { get; init; }

    /// <summary>People on the form left as they were.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Rows not written: the Check's rejections and any the write refused.</summary>
    public int RejectedCount { get; init; }

    internal static AudienceImportWrittenResponse FromDto(ImportWrittenDto dto) => new()
    {
        WrittenRowCount = dto.WrittenRowCount,
        CreatedCount = dto.CreatedCount,
        UpdatedCount = dto.UpdatedCount,
        SkippedCount = dto.SkippedCount,
        RejectedCount = dto.RejectedCount,
    };
}
