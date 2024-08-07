using Endatix.Core.UseCases.FormDefinitions.UpdateActive;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.UpdateActive;

public class UpdateActiveFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        var isDraft = true;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isActive = true;

        // Act
        Action act = () => new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceJsonData_ThrowsArgumentException()
    {
        // Arrange
        var formId = 1;
        var isDraft = true;
        var jsonData = "";
        var isActive = true;

        // Act
        Action act = () => new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(jsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        var isDraft = true;
        var jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;
        var isActive = true;

        // Act
        var command = new UpdateActiveFormDefinitionCommand(formId, isDraft, jsonData, isActive);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
        command.IsActive.Should().Be(isActive);
    }
}
