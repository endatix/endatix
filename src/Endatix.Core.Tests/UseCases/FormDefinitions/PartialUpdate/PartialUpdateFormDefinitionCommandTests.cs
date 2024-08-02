using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.PartialUpdate;

public class PartialUpdateFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateCommand()
    {
        // Arrange
        var formId = 1;
        var definitionId = 1;
        bool? isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool? isActive = true;

        // Act
        var command = new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        command.FormId.Should().Be(formId);
        command.DefinitionId.Should().Be(definitionId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
        command.IsActive.Should().Be(isActive);
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 0;
        var definitionId = 1;
        bool? isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool? isActive = true;

        // Act
        Action act = () => new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroDefinitionId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 1;
        var definitionId = 0;
        bool? isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool? isActive = true;

        // Act
        Action act = () => new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input definitionId cannot be zero or negative. (Parameter 'definitionId')");
    }
}
