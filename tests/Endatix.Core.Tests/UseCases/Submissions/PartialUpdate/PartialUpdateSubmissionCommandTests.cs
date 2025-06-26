using Endatix.Core.UseCases.Submissions.PartialUpdate;

namespace Endatix.Core.Tests.UseCases.Submissions.PartialUpdate;

public class PartialUpdateSubmissionCommandTests
{
    [Fact]
    public void Constructor_AllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        long submissionId = 10;
        long formId = 20;
        bool? isComplete = true;
        int? currentPage = 2;
        var jsonData = "{\"key\":\"value\"}";
        var metadata = "{\"meta\":\"data\"}";

        // Act
        var command = new PartialUpdateSubmissionCommand(submissionId, formId, isComplete, currentPage, jsonData, metadata);

        // Assert
        Assert.Equal(submissionId, command.SubmissionId);
        Assert.Equal(formId, command.FormId);
        Assert.Equal(isComplete, command.IsComplete);
        Assert.Equal(currentPage, command.CurrentPage);
        Assert.Equal(jsonData, command.JsonData);
        Assert.Equal(metadata, command.Metadata);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesToNull()
    {
        // Arrange & Act
        var command = new PartialUpdateSubmissionCommand(
            SubmissionId: 1,
            FormId: 2,
            IsComplete: null,
            CurrentPage: null,
            JsonData: null,
            Metadata: null
        );

        // Assert
        Assert.Equal(1, command.SubmissionId);
        Assert.Equal(2, command.FormId);
        Assert.Null(command.IsComplete);
        Assert.Null(command.CurrentPage);
        Assert.Null(command.JsonData);
        Assert.Null(command.Metadata);
    }

    [Fact]
    public void Constructor_NullJsonData_SetsPropertyToNull()
    {
        // Arrange & Act
        var command = new PartialUpdateSubmissionCommand(
            SubmissionId: 3,
            FormId: 4,
            IsComplete: false,
            CurrentPage: 1,
            JsonData: null,
            Metadata: "meta"
        );

        // Assert
        Assert.Equal(3, command.SubmissionId);
        Assert.Equal(4, command.FormId);
        Assert.False(command.IsComplete);
        Assert.Equal(1, command.CurrentPage);
        Assert.Null(command.JsonData);
        Assert.Equal("meta", command.Metadata);
    }
} 