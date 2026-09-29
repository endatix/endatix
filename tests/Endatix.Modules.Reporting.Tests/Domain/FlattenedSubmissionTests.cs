using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Domain;

public class FlattenedSubmissionTests
{
    [Fact]
    public void Constructor_StartsAsPending_WithNoData()
    {
        var row = new FlattenedSubmission(submissionId: 1, tenantId: 10, formId: 100);

        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Pending);
        row.DataJson.Should().BeNull();
        row.Integration.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkProcessed_SetsDataAndProcessedStatus()
    {
        var row = new FlattenedSubmission(1, 10, 100);

        var sourceModifiedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        row.MarkProcessed("{\"q1\":\"yes\"}", sourceModifiedAt);

        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        row.DataJson.Should().Be("{\"q1\":\"yes\"}");
        row.SourceModifiedAt.Should().Be(sourceModifiedAt);
        row.Integration.ProcessedAt.Should().NotBeNull();
        row.Integration.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkSkipped_ClearsDataAndSourceStamp()
    {
        var row = new FlattenedSubmission(1, 10, 100);
        row.MarkProcessed("{\"q1\":\"yes\"}", DateTime.UtcNow);

        row.MarkSkipped();

        row.DataJson.Should().BeNull();
        row.SourceModifiedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_StoresTruncatedError()
    {
        var row = new FlattenedSubmission(1, 10, 100);
        var error = new string('x', 2500);

        row.MarkFailed(error);

        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Failed);
        row.Integration.LastError.Should().HaveLength(SubmissionIntegrationState.MaxErrorLength);
    }

    [Fact]
    public void ToIntegrationSnapshot_DelegatesToIntegration()
    {
        var row = new FlattenedSubmission(1, 10, 100);
        row.MarkProcessed("{\"q1\":\"yes\"}", DateTime.UtcNow);

        var snapshot = row.ToIntegrationSnapshot();

        snapshot.Status.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        snapshot.ProcessedAt.Should().NotBeNull();
    }
}
