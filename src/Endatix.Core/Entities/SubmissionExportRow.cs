using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a row of submission data for export operations.
/// </summary>
public class SubmissionExportRow : IExportItem
{
    public long FormId { get; init; }
    public long Id { get; init; }
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public long? SubmitterId { get; init; }
    public string? SubmitterDisplayId { get; init; }
    public string AnswersModel { get; init; } = string.Empty;

    /// <summary>
    /// Completion duration in whole seconds when both <see cref="StartedAt"/> and
    /// <see cref="CompletedAt"/> are present; otherwise null (exported as N/A).
    /// </summary>
    public static long? CalculateDurationSeconds(DateTime? startedAt, DateTime? completedAt)
    {
        if (startedAt is null || completedAt is null)
        {
            return null;
        }

        if (completedAt < startedAt)
        {
            return null;
        }

        return (long)Math.Floor((completedAt.Value - startedAt.Value).TotalSeconds);
    }

    /// <summary>
    /// Ordered metadata columns exported from row properties.
    /// Excludes <see cref="AnswersModel"/>, which is flattened into schema-driven columns.
    /// <see cref="DurationSeconds"/> is computed at export time (not a SQL column).
    /// </summary>
    public static class SystemColumns
    {
        public const string FormId = nameof(SubmissionExportRow.FormId);
        public const string Id = nameof(SubmissionExportRow.Id);
        public const string IsComplete = nameof(SubmissionExportRow.IsComplete);
        public const string CreatedAt = nameof(SubmissionExportRow.CreatedAt);
        public const string ModifiedAt = nameof(SubmissionExportRow.ModifiedAt);
        public const string StartedAt = nameof(SubmissionExportRow.StartedAt);
        public const string CompletedAt = nameof(SubmissionExportRow.CompletedAt);
        public const string DurationSeconds = "DurationSeconds";
        public const string SubmitterId = nameof(SubmissionExportRow.SubmitterId);
        public const string SubmitterDisplayId = nameof(SubmissionExportRow.SubmitterDisplayId);

        public static IReadOnlyList<string> OrderedKeys { get; } =
        [
            FormId,
            Id,
            IsComplete,
            CreatedAt,
            ModifiedAt,
            StartedAt,
            CompletedAt,
            DurationSeconds,
            SubmitterId,
            SubmitterDisplayId,
        ];

        private static readonly HashSet<string> _keySet = new(OrderedKeys, StringComparer.Ordinal);

        public static bool Contains(string canonicalKey) => _keySet.Contains(canonicalKey);

        public static int Count => OrderedKeys.Count;
    }
}
