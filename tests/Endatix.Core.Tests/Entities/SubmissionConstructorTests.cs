using Endatix.Core.Entities;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.Entities;

public class SubmissionConstructorTests
{
    [Fact]
    public void Constructor_NullJsonData_ThrowsArgumentNullException()
    {
        // Arrange  
        var formId = 123;
        var formDefinitionId = 456;
        string? nullJsonData = null;

        // Act
        var action = () => new Submission(nullJsonData!, formId, formDefinitionId);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Null));
    }

    [Fact]
    public void Constructor_EmptyJsonData_ThrowsArgumentException()
    {
        // Arrange
        var emptyJsonData = string.Empty;
        var formId = 123;
        var formDefinitionId = 456;

        // Act
        var action = () => new Submission(emptyJsonData, formId, formDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.JsonData), Empty));
    }

    [Fact]
    public void Constructor_NegativeFormDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        const long invalidFormDefinitionId = -1;
        var form = new Form(SampleData.FORM_NAME_1){
            Id = 123 
        };
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;

        // Act
        var action = () => new Submission(jsonData, form.Id, invalidFormDefinitionId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(Submission.FormDefinitionId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidInput_SetsPropertiesCorrectly()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1){
            Id = 123
        };
        var jsonData = SampleData.SUBMISSION_JSON_DATA_1;
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 123
        };
        form.AddFormDefinition(formDefinition);

        // Act
        var submission = new Submission(jsonData, form.Id, formDefinition.Id, isComplete: false, currentPage: 2, metadata: "Test");

        // Assert
        submission.Should().NotBeNull();
        submission.JsonData.Should().Be(jsonData);
        submission.FormDefinitionId.Should().Be(formDefinition.Id);
        submission.IsComplete.Should().BeFalse();
        submission.CurrentPage.Should().Be(2);
        submission.Metadata.Should().Be("Test");
    }

    [Fact]
    public void Constructor_CompleteSubmission_SetsCompletedAt()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1){
            Id = 123    
        };
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1){
            Id = 456
        };
        form.AddFormDefinition(formDefinition);
        const string jsonData = SampleData.SUBMISSION_JSON_DATA_1;

        // Act
        var submission = new Submission(jsonData, form.Id, formDefinition.Id, isComplete: true);

        // Assert
        submission.Should().NotBeNull();
        submission.IsComplete.Should().BeTrue();
        submission.CompletedAt.Should().NotBeNull();
    }
}
