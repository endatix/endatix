namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time export filter values used for capability validation.
/// </summary>
public sealed record ExportFilterContext(
    bool? IncludeTestSubmissions,
    DateTime? CreatedAfter,
    DateTime? CreatedBefore,
    DateTime? CompletedAfter,
    DateTime? CompletedBefore,
    long? MinSubmissionId,
    long? MaxSubmissionId,
    string? Locale,
    IReadOnlyList<string>? ColumnScope);
