using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Domain;

public class FlattenedSubmissionTests
{
    private static readonly FlattenedSubmissionKey Key = new(TenantId: 10, FormId: 100, SubmissionId: 1);
    private static readonly DateTime CreatedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Fact]
    public void Constructor_StartsAsPending_WithNoData()
    {
        // Act
        var row = new FlattenedSubmission(Key, CreatedAt);

        // Assert
        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Pending);
        row.DataJson.Should().BeNull();
        row.Integration.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithKey_LocatesTheSubmission()
    {
        // Act
        var row = new FlattenedSubmission(Key, CreatedAt);

        // Assert
        row.TenantId.Should().Be(Key.TenantId);
        row.FormId.Should().Be(Key.FormId);
        row.SubmissionId.Should().Be(Key.SubmissionId);
    }

    [Fact]
    public void Constructor_WithCreatedAt_StampsCreationAndFirstAttempt()
    {
        // Act
        var row = new FlattenedSubmission(Key, CreatedAt);

        // Assert
        row.CreatedAt.Should().Be(CreatedAt);
        row.Integration.LastAttemptAt.Should().Be(CreatedAt);
        row.ModifiedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDefaultCreatedAt_Throws()
    {
        // Act
        var act = () => new FlattenedSubmission(Key, default);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("createdAt");
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcCreatedAt_Throws(DateTimeKind kind)
    {
        // Arrange
        var createdAt = DateTime.SpecifyKind(CreatedAt, kind);

        // Act
        var act = () => new FlattenedSubmission(Key, createdAt);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("createdAt");
    }

    [Fact]
    public void MarkDeleted_OnPendingRow_MarksDeletedAndLeavesModifiedAtToTheSave()
    {
        // Arrange
        var row = new FlattenedSubmission(Key, CreatedAt);

        // Act
        row.MarkDeleted();

        // Assert
        row.IsDeleted.Should().BeTrue();
        row.ModifiedAt.Should().BeNull();
    }

    [Fact]
    public void ToIntegrationSnapshot_OnPendingRow_DelegatesToIntegration()
    {
        // Arrange
        var row = new FlattenedSubmission(Key, CreatedAt);

        // Act
        var snapshot = row.ToIntegrationSnapshot();

        // Assert
        snapshot.Status.Should().Be(SubmissionIntegrationStatusCodes.Pending);
        snapshot.LastAttemptAt.Should().Be(row.Integration.LastAttemptAt);
        snapshot.ProcessedAt.Should().BeNull();
    }
}
