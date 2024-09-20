using Endatix.Core.UseCases.Forms.Update;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Forms.Update;

public class UpdateFormCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var isEnabled = true;

        // Act
        Action act = () => new UpdateFormCommand(formId, name, description, isEnabled);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var name = "";
        var description = SampleData.FORM_DESCRIPTION_1;
        var isEnabled = true;

        // Act
        Action act = () => new UpdateFormCommand(formId, name, description, isEnabled);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(name), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var name = SampleData.FORM_NAME_1;
        var description = SampleData.FORM_DESCRIPTION_1;
        var isEnabled = true;

        // Act
        var command = new UpdateFormCommand(formId, name, description, isEnabled);

        // Assert
        command.FormId.Should().Be(formId);
        command.Name.Should().Be(name);
        command.Description.Should().Be(description);
        command.IsEnabled.Should().Be(isEnabled);
    }
}
