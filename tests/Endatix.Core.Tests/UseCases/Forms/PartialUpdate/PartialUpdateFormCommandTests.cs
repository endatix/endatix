using Endatix.Core.UseCases.Forms.PartialUpdate;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var isEnabled = true;
        long? themeId = 2;

        // Act
        Action act = () => new PartialUpdateFormCommand(formId, name, description, isEnabled, themeId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        string? name = SampleData.FORM_NAME_1;
        string? description = SampleData.FORM_DESCRIPTION_1;
        bool? isEnabled = true;
        long? themeId = 2;
        // Act
        var command = new PartialUpdateFormCommand(formId, name, description, isEnabled, themeId);

        // Assert
        command.FormId.Should().Be(formId);
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.IsEnabled.Should().Be(isEnabled);
        command.ThemeId.Should().Be(themeId);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        string? name = null;
        string? description = null;
        bool? isEnabled = null;

        // Act
        var command = new PartialUpdateFormCommand(formId, name, description, isEnabled, null);

        // Assert
        command.FormId.Should().Be(formId);
        command.Name.Should().BeNull();
        command.Description.Should().BeNull();
        command.IsEnabled.Should().BeNull();
        command.ThemeId.Should().BeNull();
    }
}
