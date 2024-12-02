using Endatix.Core.UseCases.FormDefinitions.Create;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isDraft = true;

        // Act
        Action act = () => new CreateFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceJsonData_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var jsonData = "";
        var isDraft = true;

        // Act
        Action act = () => new CreateFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(jsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isDraft = true;
        
        // Act
        var command = new CreateFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
    }
}
