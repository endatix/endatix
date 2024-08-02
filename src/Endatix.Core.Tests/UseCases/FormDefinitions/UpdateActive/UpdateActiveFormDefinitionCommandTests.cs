using Endatix.Core.UseCases.FormDefinitions.UpdateActive;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateCommand()
    {
        // Arrange
        var formId = 1;
        bool isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool isActive = true;

        // Act
        var command = new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
        command.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 0;
        bool isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool isActive = true;

        // Act
        Action act = () => new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }

    [Fact]
    public void Constructor_WithNullOrWhiteSpaceJsonData_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 1;
        bool isDraft = true;
        string jsonData = " ";
        bool isActive = true;

        // Act
        Action act = () => new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input jsonData was empty. (Parameter 'jsonData')");
    }
}
