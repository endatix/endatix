using Endatix.Core.UseCases.Forms.GetById;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Forms.GetById;

public class GetFormByIdQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;

        // Act
        Action act = () => new GetFormByIdQuery(formId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameter_SetsPropertyCorrectly()
    {
        // Arrange
        var formId = 1;

        // Act
        var query = new GetFormByIdQuery(formId);

        // Assert
        query.FormId.Should().Be(formId);
    }
}
