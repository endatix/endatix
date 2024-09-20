using Endatix.Core.Entities;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.Entities;

public class SubmissionConstructorTests
{
    [Fact]
    public void Constructor_NullJsonData_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new Submission(null);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Null));
    }

    [Fact]
    public void Constructor_EmptyJsonData_ThrowsArgumentException()
    {
        // Act
        var action = () => new Submission(string.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Empty));
    }

    [Fact]
    public void Constructor_NegativeFormDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        const long invalidFormDefinitionId = -1;

        // Act
        var action = () => new Submission(jsonData, invalidFormDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.FormDefinitionId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidInput_SetsPropertiesCorrectly()
    {
        // Arrange
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        const long formDefinitionId = 123;

        // Act
        var submission = new Submission(jsonData, formDefinitionId, isComplete: false, currentPage: 2, metadata: "Test");

        // Assert
        submission.Should().NotBeNull();
        submission.JsonData.Should().Be(jsonData);
        submission.FormDefinitionId.Should().Be(formDefinitionId);
        submission.IsComplete.Should().BeFalse();
        submission.CurrentPage.Should().Be(2);
        submission.Metadata.Should().Be("Test");
    }

    [Fact]
    public void Constructor_CompleteSubmission_SetsCompletedAt()
    {
        // Arrange
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;

        // Act
        var submission = new Submission(jsonData, isComplete: true);

        // Assert
        submission.Should().NotBeNull();
        submission.IsComplete.Should().BeTrue();
        submission.CompletedAt.Should().NotBeNull();
    }
}
