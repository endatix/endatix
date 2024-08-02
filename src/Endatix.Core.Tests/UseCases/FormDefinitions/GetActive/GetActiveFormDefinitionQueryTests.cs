using Endatix.Core.UseCases.FormDefinitions.GetActive;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.GetActive;

public class GetActiveFormDefinitionQueryTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateQuery()
    {
        // Arrange
        var formId = 1;

        // Act
        var query = new GetActiveFormDefinitionQuery(formId);

        // Assert
        query.FormId.Should().Be(formId);
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 0;

        // Act
        Action act = () => new GetActiveFormDefinitionQuery(formId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }
}
