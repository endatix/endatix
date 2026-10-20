using Endatix.Infrastructure.Features.Outbox;

namespace Endatix.Modules.Reporting.Features.BackgroundJobs;

// The job types are persisted on queued jobs: never rename them.
internal sealed record ReportingCompileFormSchemaPayload(long OutboxMessageId) : IOutboxJobPayload<ReportingCompileFormSchemaPayload>
{
    public static string JobType => "ReportingCompileFormSchema";

    public static ReportingCompileFormSchemaPayload For(long outboxMessageId) => new(outboxMessageId);
}

internal sealed record ReportingFlattenSubmissionPayload(long OutboxMessageId) : IOutboxJobPayload<ReportingFlattenSubmissionPayload>
{
    public static string JobType => "ReportingFlattenSubmission";

    public static ReportingFlattenSubmissionPayload For(long outboxMessageId) => new(outboxMessageId);
}

internal sealed record ReportingSeedDefaultExportFormatsPayload(long OutboxMessageId) : IOutboxJobPayload<ReportingSeedDefaultExportFormatsPayload>
{
    public static string JobType => "ReportingSeedDefaultExportFormats";

    public static ReportingSeedDefaultExportFormatsPayload For(long outboxMessageId) => new(outboxMessageId);
}

internal sealed record ReportingSyncFormDeletionPayload(long OutboxMessageId) : IOutboxJobPayload<ReportingSyncFormDeletionPayload>
{
    public static string JobType => "ReportingSyncFormDeletion";

    public static ReportingSyncFormDeletionPayload For(long outboxMessageId) => new(outboxMessageId);
}

internal sealed record ReportingSyncSubmissionDeletionPayload(long OutboxMessageId) : IOutboxJobPayload<ReportingSyncSubmissionDeletionPayload>
{
    public static string JobType => "ReportingSyncSubmissionDeletion";

    public static ReportingSyncSubmissionDeletionPayload For(long outboxMessageId) => new(outboxMessageId);
}
