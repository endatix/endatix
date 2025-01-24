using Endatix.Core.UseCases.Forms.Delete;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Forms.Delete;

public class DeleteFormCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;

        // Act
        Action act = () => new DeleteFormCommand(formId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;

        // Act
        var command = new DeleteFormCommand(formId);

        // Assert
        command.FormId.Should().Be(formId);
    }
} 