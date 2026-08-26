using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Query options for streaming flattened submission export rows.
/// Date bounds are inclusive From / exclusive To (parsed UTC instants).
/// </summary>
public sealed record ExportQueryOptions(
    int PageSize = 500,
    long? AfterSubmissionId = null,
    bool IncludeTestSubmissions = false,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default,
    UtcDateTimeRange Started = default,
    UtcDateTimeRange Completed = default,
    long? MinSubmissionId = null,
    long? MaxSubmissionId = null,
    bool? IsComplete = null);
