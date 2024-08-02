using Endatix.Core.UseCases.FormDefinitions.GetById;
using FluentAssertions;

namespace Endatix.Core.UseCases.Tests.FormDefinitions.GetById;

public class GetFormDefinitionByIdQueryTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateQuery()
    {
        // Arrange
        var formId = 1;
        var definitionId = 1;

        // Act
        var query = new GetFormDefinitionByIdQuery(formId, definitionId);

        // Assert
        query.FormId.Should().Be(formId);
        query.DefinitionId.Should().Be(definitionId);
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroFormId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 0;
        var definitionId = 1;

        // Act
        Action act = () => new GetFormDefinitionByIdQuery(formId, definitionId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input formId cannot be zero or negative. (Parameter 'formId')");
    }

    [Fact]
    public void Constructor_WithNegativeOrZeroDefinitionId_ShouldThrowArgumentException()
    {
        // Arrange
        var formId = 1;
        var definitionId = 0;

        // Act
        Action act = () => new GetFormDefinitionByIdQuery(formId, definitionId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Required input definitionId cannot be zero or negative. (Parameter 'definitionId')");
    }
}
