using Endatix.Core.UseCases.Submissions.Create;

namespace Endatix.Core.Tests.UseCases.Submissions.Create;

public class CreateSubmissionCommandTests
{
    [Fact]
    public void Constructor_NullJsonData_SetsPropertyToNull()
    {
        // Arrange & Act
        var command = new CreateSubmissionCommand(
            FormId: 1,
            JsonData: null,
            Metadata: null,
            CurrentPage: null,
            IsComplete: null,
            ReCaptchaToken: null,
            SubmittedById: null,
            SubmittedByName: null
        );

        // Assert
        Assert.Null(command.JsonData);
        Assert.Equal(1, command.FormId);
    }

    [Fact]
    public void Constructor_AllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 2L;
        var jsonData = "{\"key\":\"value\"}";
        var metadata = "{\"meta\":\"data\"}";
        int? currentPage = 3;
        bool? isComplete = true;
        var reCaptchaToken = "test-token";
        var submittedById = "123";
        var submittedByName = "John Doe";
        // Act
        var command = new CreateSubmissionCommand(formId, jsonData, metadata, currentPage, isComplete, reCaptchaToken, submittedById, submittedByName);

        // Assert
        Assert.Equal(formId, command.FormId);
        Assert.Equal(jsonData, command.JsonData);
        Assert.Equal(metadata, command.Metadata);
        Assert.Equal(currentPage, command.CurrentPage);
        Assert.Equal(isComplete, command.IsComplete);
        Assert.Equal(reCaptchaToken, command.ReCaptchaToken);
        Assert.Equal(submittedById, command.SubmittedById);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesToNull()
    {
        // Arrange & Act
        var command = new CreateSubmissionCommand(
            FormId: 5,
            JsonData: null,
            Metadata: null,
            CurrentPage: null,
            IsComplete: null,
            ReCaptchaToken: null,
            SubmittedById: null,
            SubmittedByName: null
        );

        // Assert
        Assert.Equal(5, command.FormId);
        Assert.Null(command.JsonData);
        Assert.Null(command.Metadata);
        Assert.Null(command.CurrentPage);
        Assert.Null(command.IsComplete);
        Assert.Null(command.SubmittedById);
    }
}
