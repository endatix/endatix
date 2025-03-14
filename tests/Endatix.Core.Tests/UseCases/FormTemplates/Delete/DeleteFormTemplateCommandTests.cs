using Endatix.Core.UseCases.FormTemplates.Delete;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormTemplates.Delete;

public class DeleteFormTemplateCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var formTemplateId = -1;

        // Act
        Action act = () => new DeleteFormTemplateCommand(formTemplateId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formTemplateId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formTemplateId = 1;

        // Act
        var command = new DeleteFormTemplateCommand(formTemplateId);

        // Assert
        command.FormTemplateId.Should().Be(formTemplateId);
    }
} 