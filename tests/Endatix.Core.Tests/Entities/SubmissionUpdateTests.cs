using Endatix.Core.Entities;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.Entities;

public class SubmissionUpdateTests
{
    [Fact]
    public void Update_NullJsonData_ThrowsArgumentNullException()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, SampleData.SUBMISSION_JSON_DATA_1, formId: 123, formDefinitionId: 456);

        // Act
        var action = () => submission.Update(null, formDefinitionId: 123);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Null));
    }

    [Fact]
    public void Update_EmptyJsonData_ThrowsArgumentException()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, SampleData.SUBMISSION_JSON_DATA_1, formId: 123, formDefinitionId: 456);

        // Act
        var action = () => submission.Update(string.Empty, formDefinitionId: 123);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Empty));
    }

    [Fact]
    public void Update_NegativeFormDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, SampleData.SUBMISSION_JSON_DATA_1, formId: 123, formDefinitionId: 456);
        const long invalidFormDefinitionId = -1;

        // Act
        var action = () => submission.Update(SampleData.SUBMISSION_JSON_DATA_1, invalidFormDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.FormDefinitionId), ZeroOrNegative));
    }

    [Fact]
    public void Update_ValidInput_UpdatesPropertiesCorrectly()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, SampleData.SUBMISSION_JSON_DATA_1, formId: 123, formDefinitionId: 456, isComplete: false);
        const string updatedJsonData = SampleData.SUBMISSION_JSON_DATA_2;
        const long updatedFormDefinitionId = 789;

        // Act
        submission.Update(updatedJsonData, updatedFormDefinitionId, isComplete: false, currentPage: 3, metadata: "Updated");

        // Assert
        submission.Should().NotBeNull();
        submission.JsonData.Should().Be(updatedJsonData);
        submission.FormDefinitionId.Should().Be(updatedFormDefinitionId);
        submission.CurrentPage.Should().Be(3);
        submission.Metadata.Should().Be("Updated");
        submission.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void Update_CompleteSubmission_UpdatesCompletedAt()
    {
        // Arrange
        var submission = new Submission(SampleData.TENANT_ID, SampleData.SUBMISSION_JSON_DATA_1, formId: 123, formDefinitionId: 456);

        // Act
        submission.Update(SampleData.SUBMISSION_JSON_DATA_1, formDefinitionId: 123, isComplete: true);

        // Assert
        submission.Should().NotBeNull();
        submission.IsComplete.Should().BeTrue();
        submission.CompletedAt.Should().NotBeNull();
    }
}
