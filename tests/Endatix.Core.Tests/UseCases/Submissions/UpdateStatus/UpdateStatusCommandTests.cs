using Endatix.Core.UseCases.Submissions.UpdateStatus;

namespace Endatix.Core.Tests.UseCases.Submissions.UpdateStatus;

public class UpdateStatusCommandTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        const long submissionId = 1;
        const long formId = 2;
        const string statusCode = "new";

        // Act
        var command = new UpdateStatusCommand(submissionId, formId, statusCode);

        // Assert
        command.SubmissionId.Should().Be(submissionId);
        command.FormId.Should().Be(formId);
        command.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public void Constructor_WithDifferentValues_ShouldCreateDifferentInstances()
    {
        // Arrange
        var command1 = new UpdateStatusCommand(1, 1, "new");
        var command2 = new UpdateStatusCommand(2, 2, "approved");

        // Assert
        command1.Should().NotBe(command2);
    }

    [Fact]
    public void Constructor_WithSameValues_ShouldCreateEqualInstances()
    {
        // Arrange
        var command1 = new UpdateStatusCommand(1, 1, "new");
        var command2 = new UpdateStatusCommand(1, 1, "new");

        // Assert
        command1.Should().Be(command2);
    }

    [Theory]
    [InlineData(1, 1, "new")]
    [InlineData(2, 3, "approved")]
    public void Constructor_WithVariousValidInputs_ShouldCreateValidInstances(
        long submissionId,
        long formId,
        string statusCode)
    {
        // Act
        var command = new UpdateStatusCommand(submissionId, formId, statusCode);

        // Assert
        command.Should().NotBeNull();
        command.SubmissionId.Should().Be(submissionId);
        command.FormId.Should().Be(formId);
        command.StatusCode.Should().Be(statusCode);
    }
} 