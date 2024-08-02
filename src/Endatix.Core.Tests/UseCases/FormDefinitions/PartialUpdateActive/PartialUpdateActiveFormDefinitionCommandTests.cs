using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateCommand()
    {
        // Arrange
        var formId = 1;
        bool? isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool? isActive = true;

        // Act
        var command = new PartialUpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

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
        bool? isDraft = true;
        string jsonData = "{\"key\":\"value\"}";
        bool? isActive = true;

        // Act
        Action act = () => new PartialUpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }
}
