using Endatix.Core.UseCases.FormTemplates.GetById;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormTemplates.GetById;

public class GetFormTemplateByIdQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var formTemplateId = -1;

        // Act
        Action act = () => new GetFormTemplateByIdQuery(formTemplateId);

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
        var query = new GetFormTemplateByIdQuery(formTemplateId);

        // Assert
        query.FormTemplateId.Should().Be(formTemplateId);
    }
} 