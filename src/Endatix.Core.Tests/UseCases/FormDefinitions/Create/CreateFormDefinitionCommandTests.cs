using Endatix.Core.UseCases.FormDefinitions.Create;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.Create;

public class CreateFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateCommand()
    {
        // Arrange
        var formId = 1;
        var isDraft = true;
        var jsonData = "{\"key\":\"value\"}";
        var isActive = true;

        // Act
        var command = new CreateFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
        command.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void Constructor_WithNegativeFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = -1;
        var isDraft = true;
        var jsonData = "{\"key\":\"value\"}";
        var isActive = true;

        // Act
        Action act = () => new CreateFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }

    [Fact]
    public void Constructor_WithNullOrWhiteSpaceJsonData_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 1;
        var isDraft = true;
        var jsonData = " ";
        var isActive = true;

        // Act
        Action act = () => new CreateFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input jsonData was empty. (Parameter 'jsonData')");
    }
}
