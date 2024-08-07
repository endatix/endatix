using Endatix.Core.UseCases.Forms.Create;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Forms.Create;

public class CreateFormCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var description = "Description";
        var isEnabled = true;
        var formDefinitionJsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;

        // Act
        Action act = () => new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(name), Empty));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceFormDefinitionJsonData_ThrowsArgumentException()
    {
        // Arrange
        var name = "Form Name";
        var description = "Description";
        var isEnabled = true;
        var formDefinitionJsonData = "";

        // Act
        Action act = () => new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formDefinitionJsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Form Name";
        var description = "Description";
        var isEnabled = true;
        var formDefinitionJsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;

        // Act
        var command = new CreateFormCommand(name, description, isEnabled, formDefinitionJsonData);

        // Assert
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.IsEnabled.Should().Be(isEnabled);
        command.FormDefinitionJsonData.Should().Be(formDefinitionJsonData);
    }
}
