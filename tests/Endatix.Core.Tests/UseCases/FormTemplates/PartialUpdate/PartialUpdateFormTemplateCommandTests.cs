using Endatix.Core.UseCases.FormTemplates.PartialUpdate;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormTemplates.PartialUpdate;

public class PartialUpdateFormTemplateCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var formTemplateId = -1;
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isEnabled = true;

        // Act
        Action act = () => new PartialUpdateFormTemplateCommand(formTemplateId, name, description, jsonData, isEnabled);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formTemplateId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formTemplateId = 1;
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isEnabled = true;

        // Act
        var command = new PartialUpdateFormTemplateCommand(formTemplateId, name, description, jsonData, isEnabled);

        // Assert
        command.FormTemplateId.Should().Be(formTemplateId);
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.JsonData.Should().Be(jsonData);
        command.IsEnabled.Should().Be(isEnabled);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formTemplateId = 1;
        string? name = null;
        string? description = null;
        string? jsonData = null;
        bool? isEnabled = null;

        // Act
        var command = new PartialUpdateFormTemplateCommand(formTemplateId, name, description, jsonData, isEnabled);

        // Assert
        command.FormTemplateId.Should().Be(formTemplateId);
        command.Name.Should().BeNull();
        command.Description.Should().BeNull();
        command.JsonData.Should().BeNull();
        command.IsEnabled.Should().BeNull();
    }
} 