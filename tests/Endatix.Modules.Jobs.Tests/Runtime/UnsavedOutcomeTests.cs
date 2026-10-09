using Endatix.Modules.Jobs.Runtime;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class UnsavedOutcomeTests
{
    private static readonly DateTime ClaimedAt = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime EndedAt = ClaimedAt.AddSeconds(4);

    public static TheoryData<string> UnreadableValues => new()
    {
        "not json",
        "{}",
        """{"attempt":2,"write":"Retrying","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
        """{"attempt":2,"write":"Failed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
        """{"attempt":0,"write":"Completed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
        """{"attempt":2,"write":"Completed","claimedAt":"2026-10-09T12:00:00","endedAt":"2026-10-09T12:00:04"}""",
        "null",
    };

    [Theory]
    [InlineData(nameof(AttemptRowWrite.Completed), "")]
    [InlineData(nameof(AttemptRowWrite.Failed), "Form 12 has no schema.")]
    public void TryRead_SerializedOutcome_ReturnsTheSameOutcome(string writeName, string errorMessage)
    {
        // Arrange
        var outcome = new UnsavedOutcome(
            3, Enum.Parse<AttemptRowWrite>(writeName), errorMessage.Length == 0 ? null : errorMessage, ClaimedAt, EndedAt);
        var data = new JobDataMap { [UnsavedOutcome.DataKey] = outcome.Serialize() };

        // Act
        var read = UnsavedOutcome.TryRead(data);

        // Assert
        read.Should().Be(outcome);
        read.EndedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void TryRead_TriggerWithoutOutcome_ReturnsNull()
    {
        // Arrange — a re-fire as earlier versions stored it.
        var data = new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = "42",
            [BackgroundJobExecution.ReclaimKey] = bool.TrueString,
        };

        // Act
        var read = UnsavedOutcome.TryRead(data);

        // Assert
        read.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(UnreadableValues))]
    public void TryRead_UnreadableValue_ReturnsNull(string value)
    {
        // Arrange
        var data = new JobDataMap { [UnsavedOutcome.DataKey] = value };

        // Act
        var read = UnsavedOutcome.TryRead(data);

        // Assert
        read.Should().BeNull();
    }

    [Fact]
    public void Constructor_MessageLongerThanTheColumn_CutsItToTheColumnLength()
    {
        // Arrange
        var message = new string('x', BackgroundJobStateRepository.ErrorMessageMaxLength + 10);

        // Act
        var outcome = new UnsavedOutcome(1, AttemptRowWrite.Failed, message, ClaimedAt, EndedAt);

        // Assert
        outcome.ErrorMessage.Should().HaveLength(BackgroundJobStateRepository.ErrorMessageMaxLength);
    }

    [Theory]
    [InlineData(nameof(AttemptRowWrite.Completed), true)]
    [InlineData(nameof(AttemptRowWrite.Failed), true)]
    [InlineData(nameof(AttemptRowWrite.Retrying), false)]
    [InlineData(nameof(AttemptRowWrite.DeadLettered), false)]
    [InlineData(nameof(AttemptRowWrite.None), false)]
    public void Carries_EachRowWrite_CarriesOnlyTheHandlersOwnVerdicts(string writeName, bool expected)
    {
        // Arrange
        var write = Enum.Parse<AttemptRowWrite>(writeName);

        // Act
        var carries = UnsavedOutcome.Carries(write);

        // Assert
        carries.Should().Be(expected);
    }
}
