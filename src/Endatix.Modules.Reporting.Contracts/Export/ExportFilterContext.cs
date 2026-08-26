using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time export filter values used for capability validation.
/// Date fields are parsed UTC instant ranges (inclusive From / exclusive To).
/// </summary>
public sealed record ExportFilterContext(
    bool? IncludeTestSubmissions,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default,
    UtcDateTimeRange Started = default,
    UtcDateTimeRange Completed = default,
    long? MinSubmissionId = null,
    long? MaxSubmissionId = null,
    string? Locale = null,
    IReadOnlyList<string>? ColumnScope = null,
    ExportCompletionStatus? CompletionStatus = null);
