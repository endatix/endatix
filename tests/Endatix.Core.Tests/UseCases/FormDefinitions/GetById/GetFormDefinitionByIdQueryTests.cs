using Endatix.Core.UseCases.FormDefinitions.GetById;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetById;

public class GetFormDefinitionByIdQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var definitionId = 1;

        // Act
        Action act = () => new GetFormDefinitionByIdQuery(formId, definitionId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NegativeOrZeroDefinitionId_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var definitionId = -1;

        // Act
        Action act = () => new GetFormDefinitionByIdQuery(formId, definitionId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(definitionId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
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
}
