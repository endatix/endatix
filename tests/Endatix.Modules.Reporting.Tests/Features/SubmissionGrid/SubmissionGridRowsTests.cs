using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class SubmissionGridRowsTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ModifiedAt = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RowStateOf_WithoutPreparedRow_IsNone()
    {
        // Act
        var state = SubmissionGridRows.RowStateOf(Submission(modifiedAt: null), prepared: null);

        // Assert
        state.Should().Be(SubmissionGridRowStates.None);
    }

    [Fact]
    public void RowStateOf_ProcessedFromCurrentVersion_IsPrepared()
    {
        // Arrange
        var submission = Submission(modifiedAt: ModifiedAt);

        // Act
        var state = SubmissionGridRows.RowStateOf(submission, Prepared(ModifiedAt));

        // Assert
        state.Should().Be(SubmissionGridRowStates.Prepared);
    }

    [Fact]
    public void RowStateOf_NeverModifiedSubmission_ComparesWithCreatedAt()
    {
        // Act
        var state = SubmissionGridRows.RowStateOf(Submission(modifiedAt: null), Prepared(CreatedAt));

        // Assert
        state.Should().Be(SubmissionGridRowStates.Prepared);
    }

    [Fact]
    public void RowStateOf_ProcessedFromOlderVersion_IsStale()
    {
        // Act
        var state = SubmissionGridRows.RowStateOf(Submission(modifiedAt: ModifiedAt), Prepared(CreatedAt));

        // Assert
        state.Should().Be(SubmissionGridRowStates.Stale);
    }

    [Theory]
    [InlineData(SubmissionIntegrationStatusCodes.Pending, SubmissionGridRowStates.Pending)]
    [InlineData(SubmissionIntegrationStatusCodes.Processing, SubmissionGridRowStates.Pending)]
    [InlineData(SubmissionIntegrationStatusCodes.NotProcessed, SubmissionGridRowStates.Pending)]
    [InlineData(SubmissionIntegrationStatusCodes.Failed, SubmissionGridRowStates.Failed)]
    [InlineData(SubmissionIntegrationStatusCodes.Skipped, SubmissionGridRowStates.Skipped)]
    public void RowStateOf_NotProcessed_FollowsTheStatus(string statusCode, string expected)
    {
        // Arrange
        PreparedRow prepared = new(1, statusCode, HasData: false, DataJson: null, SourceModifiedAt: null);

        // Act
        var state = SubmissionGridRows.RowStateOf(Submission(modifiedAt: null), prepared);

        // Assert
        state.Should().Be(expected);
    }

    [Fact]
    public void PickAnswers_LeavesOutMissingAndNullKeys()
    {
        // Act
        var answers = SubmissionGridRows.PickAnswers("""{"q1":"a","q2":null,"q3":3}""", ["q1", "q2", "q4"]);

        // Assert
        answers.Keys.Should().Equal("q1");
        answers["q1"].GetString().Should().Be("a");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("")]
    public void PickAnswers_AnswersThatAreNotAnObject_GiveNone(string json)
    {
        // Act
        var answers = SubmissionGridRows.PickAnswers(json, ["q1"]);

        // Assert
        answers.Should().BeEmpty();
    }

    [Fact]
    public void Compose_PreparedRow_ReadsPreparedAnswers()
    {
        // Arrange
        var submission = Submission(modifiedAt: ModifiedAt, jsonData: """{"q1":"raw"}""");
        var prepared = Prepared(ModifiedAt, dataJson: """{"q1":"prepared"}""");

        // Act
        var row = SubmissionGridRows.Compose(submission, prepared, ["q1"]);

        // Assert
        row.AnswersSource.Should().Be(SubmissionGridAnswerSources.Prepared);
        row.Answers["q1"].GetString().Should().Be("prepared");
    }

    [Fact]
    public void Compose_StaleRow_ReadsTheSubmissionsOwnAnswers()
    {
        // Arrange
        var submission = Submission(modifiedAt: ModifiedAt, jsonData: """{"q1":"raw"}""");
        var prepared = Prepared(CreatedAt, dataJson: """{"q1":"old"}""");

        // Act
        var row = SubmissionGridRows.Compose(submission, prepared, ["q1"]);

        // Assert
        row.RowState.Should().Be(SubmissionGridRowStates.Stale);
        row.AnswersSource.Should().Be(SubmissionGridAnswerSources.Raw);
        row.Answers["q1"].GetString().Should().Be("raw");
    }

    private static PreparedRow Prepared(DateTime sourceModifiedAt, string dataJson = "{}") =>
        new(1, SubmissionIntegrationStatusCodes.Processed, HasData: true, dataJson, sourceModifiedAt);

    private static SubmissionDto Submission(DateTime? modifiedAt, string jsonData = "{}") =>
        new(
            Id: 1,
            IsComplete: true,
            JsonData: jsonData,
            FormId: 10,
            FormDefinitionId: 20,
            CurrentPage: null,
            CompletedAt: null,
            StartedAt: null,
            CreatedAt: CreatedAt,
            ModifiedAt: modifiedAt,
            Metadata: null,
            Status: "new",
            SubmittedBy: null,
            SubmitterId: null,
            SubmitterDisplayId: null,
            SubmitterProfileSnapshot: null,
            IsTestSubmission: false);
}
