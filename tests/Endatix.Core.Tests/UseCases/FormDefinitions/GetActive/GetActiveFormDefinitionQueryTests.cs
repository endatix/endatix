using Endatix.Core.UseCases.FormDefinitions.GetActive;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;

        // Act
        Action act = () => new GetActiveFormDefinitionQuery(formId);

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
        var query = new GetActiveFormDefinitionQuery(formId);

        // Assert
        query.FormId.Should().Be(formId);
    }
}
