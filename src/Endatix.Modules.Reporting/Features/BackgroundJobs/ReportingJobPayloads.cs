using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Reporting.Features.BackgroundJobs;

/// <summary>The input of every Reporting job: the outbox message whose event it processes.</summary>
internal interface IReportingOutboxJobPayload : IBackgroundJobPayload
{
    long OutboxMessageId { get; }
}

internal sealed record ReportingCompileFormSchemaPayload(long OutboxMessageId) : IReportingOutboxJobPayload
{
    public static string JobType => "ReportingCompileFormSchema";
}

internal sealed record ReportingFlattenSubmissionPayload(long OutboxMessageId) : IReportingOutboxJobPayload
{
    public static string JobType => "ReportingFlattenSubmission";
}

internal sealed record ReportingSeedDefaultExportFormatsPayload(long OutboxMessageId) : IReportingOutboxJobPayload
{
    public static string JobType => "ReportingSeedDefaultExportFormats";
}

internal sealed record ReportingSyncFormDeletionPayload(long OutboxMessageId) : IReportingOutboxJobPayload
{
    public static string JobType => "ReportingSyncFormDeletion";
}

internal sealed record ReportingSyncSubmissionDeletionPayload(long OutboxMessageId) : IReportingOutboxJobPayload
{
    public static string JobType => "ReportingSyncSubmissionDeletion";
}
