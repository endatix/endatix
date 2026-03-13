namespace Endatix.Core.UseCases.Stats.Models;

public record TenantStorageStats(
    long TenantId,
    long SubmissionCount,
    long VersionCount,
    long EstimatedStorageBytes
);

public record FormStorageStats(
    long TenantId,
    long FormId,
    string FormName,
    long SubmissionCount,
    long VersionCount,
    long EstimatedStorageBytes
);

public record TableStorageStats(
    string TableName,
    long TableSizeBytes,
    long IndexSizeBytes,
    long TotalSizeBytes
);

public record StorageDashboardModel(
    TenantStorageStats TenantStats,
    IReadOnlyList<FormStorageStats> FormStats,
    IReadOnlyList<TableStorageStats> TableStats
);
