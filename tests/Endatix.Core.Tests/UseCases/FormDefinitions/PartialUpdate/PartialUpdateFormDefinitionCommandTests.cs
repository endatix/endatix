using Endatix.Core.UseCases.FormDefinitions.PartialUpdate;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdate;

public class PartialUpdateFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var definitionId = 1;
        bool? isDraft = true;
        string? jsonData = null;

        // Act
        Action act = () => new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData);

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
        bool? isDraft = true;
        string? jsonData = null;

        // Act
        Action act = () => new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData);

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
        bool? isDraft = true;
        string? jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;

        // Act
        var command = new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData);

        // Assert
        command.FormId.Should().Be(formId);
        command.DefinitionId.Should().Be(definitionId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var definitionId = 1;
        bool? isDraft = null;
        string? jsonData = null;

        // Act
        var command = new PartialUpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData);

        // Assert
        command.FormId.Should().Be(formId);
        command.DefinitionId.Should().Be(definitionId);
        command.IsDraft.Should().BeNull();
        command.JsonData.Should().BeNull();
    }
}
