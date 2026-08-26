namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time export filter values used for capability validation.
/// Date fields are wire calendar-day strings (<c>YYYY-MM-DD</c>).
/// </summary>
public sealed record ExportFilterContext(
    bool? IncludeTestSubmissions,
    string? CreatedFrom,
    string? CreatedTo,
    string? ModifiedFrom,
    string? ModifiedTo,
    string? StartedFrom,
    string? StartedTo,
    string? CompletedFrom,
    string? CompletedTo,
    long? MinSubmissionId,
    long? MaxSubmissionId,
    string? Locale,
    IReadOnlyList<string>? ColumnScope,
    ExportCompletionStatus? CompletionStatus = null);
