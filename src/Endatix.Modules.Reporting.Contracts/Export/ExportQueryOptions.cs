namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Query options for streaming flattened submission export rows.
/// </summary>
public sealed record ExportQueryOptions(
    int PageSize = 500,
    long? AfterSubmissionId = null,
    bool IncludeTestSubmissions = false,
    DateTime? CreatedAfter = null,
    DateTime? CreatedBefore = null,
    DateTime? CompletedAfter = null,
    DateTime? CompletedBefore = null,
    long? MinSubmissionId = null,
    long? MaxSubmissionId = null);
