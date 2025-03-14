using Endatix.Core.UseCases.FormTemplates.Create;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormTemplates.Create;

public class CreateFormTemplateCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var description = "Description";
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isEnabled = true;

        // Act
        Action act = () => new CreateFormTemplateCommand(name!, description, jsonData, isEnabled);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(name), Empty));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceJsonData_ThrowsArgumentException()
    {
        // Arrange
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var jsonData = "";
        var isEnabled = true;

        // Act
        Action act = () => new CreateFormTemplateCommand(name, description, jsonData!, isEnabled);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(jsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isEnabled = true;

        // Act
        var command = new CreateFormTemplateCommand(name, description, jsonData, isEnabled);

        // Assert
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.JsonData.Should().Be(jsonData);
        command.IsEnabled.Should().Be(isEnabled);
    }
} 