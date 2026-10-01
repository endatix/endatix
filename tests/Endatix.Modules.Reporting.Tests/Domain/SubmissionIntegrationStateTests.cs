using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Domain;

public class SubmissionIntegrationStateTests
{
    [Theory]
    [InlineData("processed")]
    [InlineData("processing")]
    [InlineData("failed")]
    [InlineData("not_processed")]
    public void FromCode_WithKnownCode_ReturnsState(string code)
    {
        var state = SubmissionIntegrationState.FromCode(code);

        state.Code.Should().Be(code);
    }

    [Fact]
    public void FromCode_WithBusinessStatusCode_Throws()
    {
        Action act = () => SubmissionIntegrationState.FromCode("approved");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Processed_IsExportable()
    {
        var processed = SubmissionIntegrationState.FromCode(SubmissionIntegrationStatusCodes.Processed);
        var failed = SubmissionIntegrationState.FromCode(SubmissionIntegrationStatusCodes.Failed);

        processed.IsExportable.Should().BeTrue();
        failed.IsExportable.Should().BeFalse();
    }

    [Fact]
    public void TruncateError_OverMaxLength_KeepsMaxLengthPrefix()
    {
        // Arrange
        var error = new string('x', SubmissionIntegrationState.MaxErrorLength) + "tail";

        // Act
        var stored = SubmissionIntegrationState.TruncateError(error);

        // Assert
        stored.Should().Be(new string('x', SubmissionIntegrationState.MaxErrorLength));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TruncateError_WithBlankError_ReturnsNull(string? error)
    {
        // Act
        var stored = SubmissionIntegrationState.TruncateError(error);

        // Assert
        stored.Should().BeNull();
    }

    [Fact]
    public void ToSnapshot_OnPendingState_MapsAllFields()
    {
        // Arrange
        var attemptedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var state = SubmissionIntegrationState.CreatePending(attemptedAt);

        // Act
        var snapshot = state.ToSnapshot();

        // Assert
        snapshot.Status.Should().Be(SubmissionIntegrationStatusCodes.Pending);
        snapshot.LastAttemptAt.Should().Be(attemptedAt);
        snapshot.ProcessedAt.Should().BeNull();
        snapshot.LastError.Should().BeNull();
    }
}
