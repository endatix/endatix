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
    public void ToIntegrationSnapshot_OnPendingRow_DelegatesToIntegration()
    {
        // Arrange
        var row = new FlattenedSubmission(1, 10, 100);

        // Act
        var snapshot = row.ToIntegrationSnapshot();

        // Assert
        snapshot.Status.Should().Be(SubmissionIntegrationStatusCodes.Pending);
        snapshot.LastAttemptAt.Should().Be(row.Integration.LastAttemptAt);
        snapshot.ProcessedAt.Should().BeNull();
    }
}
