using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class SubmissionExportRowDurationSecondsTests
{
    [Fact]
    public void CalculateDurationSeconds_WithBothTimestamps_ReturnsFloorSeconds()
    {
        // Arrange
        var startedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddSeconds(90.9);

        // Act
        long? duration = SubmissionExportRow.CalculateDurationSeconds(startedAt, completedAt);

        // Assert
        duration.Should().Be(90);
    }

    [Fact]
    public void CalculateDurationSeconds_WhenIncomplete_ReturnsNull()
    {
        // Arrange
        var startedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Act
        long? duration = SubmissionExportRow.CalculateDurationSeconds(startedAt, completedAt: null);

        // Assert
        duration.Should().BeNull();
    }

    [Fact]
    public void CalculateDurationSeconds_WhenCompletedBeforeStarted_ReturnsNull()
    {
        // Arrange
        var startedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddSeconds(-1);

        // Act
        long? duration = SubmissionExportRow.CalculateDurationSeconds(startedAt, completedAt);

        // Assert
        duration.Should().BeNull();
    }
}
