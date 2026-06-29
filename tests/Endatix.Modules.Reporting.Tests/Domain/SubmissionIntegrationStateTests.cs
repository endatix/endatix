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
    public void MarkFailed_TruncatesError()
    {
        var state = SubmissionIntegrationState.CreatePending(DateTime.UtcNow);

        state.MarkFailed(new string('x', 2500));

        state.LastError.Should().HaveLength(SubmissionIntegrationState.MaxErrorLength);
        state.Code.Should().Be(SubmissionIntegrationStatusCodes.Failed);
    }

    [Fact]
    public void ToSnapshot_MapsAllFields()
    {
        var state = SubmissionIntegrationState.CreatePending(DateTime.UtcNow);
        state.MarkProcessed();

        var snapshot = state.ToSnapshot();

        snapshot.Status.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        snapshot.ProcessedAt.Should().NotBeNull();
        snapshot.LastAttemptAt.Should().NotBeNull();
        snapshot.LastError.Should().BeNull();
    }
}
