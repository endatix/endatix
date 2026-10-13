using Endatix.Modules.Jobs.Runtime;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class UnsavedOutcomeTests
{
    private static readonly DateTime ClaimedAt = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime EndedAt = ClaimedAt.AddSeconds(4);

    public static TheoryData<string, string> UnreadableValues => new()
    {
        { "not json", "does not parse as an outcome at $" },
        { "{}", "its attempt is not positive" },
        {
            """{"attempt":2,"write":"Retrying","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
            "its write Retrying is not one an attempt's outcome makes"
        },
        {
            """{"attempt":2,"write":"Failed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
            "its failure has no message"
        },
        {
            """{"attempt":0,"write":"Completed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
            "its attempt is not positive"
        },
        {
            """{"attempt":2,"write":"Completed","claimedAt":"2026-10-09T12:00:00","endedAt":"2026-10-09T12:00:04"}""",
            "its times are not UTC"
        },
        {
            """{"attempt":2,"write":"Completed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z","rejections":-1}""",
            "its rejection count is negative"
        },
        { """{"attempt":"two"}""", "does not parse as an outcome at $.attempt" },
        { "null", "it is empty" },
    };

    [Theory]
    [InlineData(nameof(AttemptRowWrite.Completed), "")]
    [InlineData(nameof(AttemptRowWrite.Failed), "Form 12 has no schema.")]
    public void Read_SerializedOutcome_ReturnsTheSameOutcome(string writeName, string errorMessage)
    {
        // Arrange
        var outcome = new UnsavedOutcome(
            3, Enum.Parse<AttemptRowWrite>(writeName), errorMessage.Length == 0 ? null : errorMessage, ClaimedAt, EndedAt);
        var data = new JobDataMap { [UnsavedOutcome.DataKey] = outcome.Serialize() };

        // Act
        var read = UnsavedOutcome.Read(data).Outcome;

        // Assert
        read.Should().Be(outcome);
        read.EndedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [MemberData(nameof(UnreadableValues))]
    public void Read_UnreadableValue_ReturnsNoOutcomeAndWhy(string value, string reason)
    {
        // Arrange
        var data = new JobDataMap { [UnsavedOutcome.DataKey] = value };

        // Act
        var read = UnsavedOutcome.Read(data);

        // Assert
        read.Outcome.Should().BeNull();
        read.Unreadable.Should().Contain(reason);
    }

    [Fact]
    public void Read_TriggerWithoutOutcome_ReturnsNeitherOutcomeNorWhy()
    {
        // Arrange — a re-fire as earlier versions stored it.
        var data = new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = "42",
            [BackgroundJobExecution.ReclaimKey] = bool.TrueString,
        };

        // Act
        var read = UnsavedOutcome.Read(data);

        // Assert
        read.Should().Be(default(CarriedOutcome));
    }

    [Fact]
    public void Read_OutcomeStoredWithoutRejections_ReadsItAsNeverRejected()
    {
        // Arrange — an outcome as it was stored before rejections were counted.
        var data = new JobDataMap
        {
            [UnsavedOutcome.DataKey] =
                """{"attempt":2,"write":"Completed","claimedAt":"2026-10-09T12:00:00Z","endedAt":"2026-10-09T12:00:04Z"}""",
        };

        // Act
        var read = UnsavedOutcome.Read(data);

        // Assert
        read.Outcome.Should().Be(new UnsavedOutcome(2, AttemptRowWrite.Completed, null, ClaimedAt, EndedAt));
    }

    [Fact]
    public void Read_RejectedOutcome_KeepsItsRejections()
    {
        // Arrange
        var outcome = new UnsavedOutcome(2, AttemptRowWrite.Completed, null, ClaimedAt, EndedAt).Rejected().Rejected();
        var data = new JobDataMap { [UnsavedOutcome.DataKey] = outcome.Serialize() };

        // Act
        var read = UnsavedOutcome.Read(data).Outcome;

        // Assert
        read.Should().Be(outcome);
        read.Rejections.Should().Be(2);
    }

    [Theory]
    [InlineData(UnsavedOutcome.MaxRejections - 1, false)]
    [InlineData(UnsavedOutcome.MaxRejections, true)]
    public void IsSpent_AfterRejections_IsSpentOnlyAtTheMost(int rejections, bool expected)
    {
        // Arrange
        var outcome = new UnsavedOutcome(2, AttemptRowWrite.Completed, null, ClaimedAt, EndedAt) { Rejections = rejections };

        // Act
        var spent = outcome.IsSpent;

        // Assert
        spent.Should().Be(expected);
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
