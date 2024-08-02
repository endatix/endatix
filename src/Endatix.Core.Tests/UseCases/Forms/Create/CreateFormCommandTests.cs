using Endatix.Core.UseCases.Forms.Create;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.Forms.Create;

public class CreateFormCommandTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateCommand()
    {
        // Arrange
        string name = "Test Form";
        string description = "Test Description";
        bool isEnabled = true;
        string formDefinitionJsonData = "{\"key\":\"value\"}";

        // Act
        var command = new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.IsEnabled.Should().Be(isEnabled);
        command.FormDefinitionJsonData.Should().Be(formDefinitionJsonData);
    }

    [Fact]
    public void Constructor_WithNullOrWhiteSpaceName_ShouldThrowArgumentException()
    {
        // Arrange
        string name = " ";
        string description = "Test Description";
        bool isEnabled = true;
        string formDefinitionJsonData = "{\"key\":\"value\"}";

        // Act
        Action act = () => new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input name was empty. (Parameter 'name')");
    }

    [Fact]
    public void Constructor_WithNullOrWhiteSpaceFormDefinitionJsonData_ShouldThrowArgumentException()
    {
        // Arrange
        string name = "Test Form";
        string description = "Test Description";
        bool isEnabled = true;
        string formDefinitionJsonData = " ";

        // Act
        Action act = () => new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formDefinitionJsonData was empty. (Parameter 'formDefinitionJsonData')");
    }
}
