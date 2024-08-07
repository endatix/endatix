using Endatix.Core.UseCases.FormDefinitions.Update;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.Update;

public class UpdateFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var definitionId = 1;
        var isDraft = true;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isActive = true;

        // Act
        Action act = () => new UpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

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
        var isDraft = true;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isActive = true;

        // Act
        Action act = () => new UpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(definitionId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceJsonData_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var definitionId = 1;
        var isDraft = true;
        var jsonData = "";
        var isActive = true;

        // Act
        Action act = () => new UpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(jsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var definitionId = 1;
        var isDraft = true;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isActive = true;

        // Act
        var command = new UpdateFormDefinitionCommand(formId, definitionId, isDraft, jsonData, isActive);

        // Assert
        command.FormId.Should().Be(formId);
        command.DefinitionId.Should().Be(definitionId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
        command.IsActive.Should().Be(isActive);
    }
}
