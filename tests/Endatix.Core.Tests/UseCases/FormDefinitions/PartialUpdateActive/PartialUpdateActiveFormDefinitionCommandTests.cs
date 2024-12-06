using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionCommandTests
{
    [Fact]
    public void Constructor_NegativeOrZeroFormId_ThrowsArgumentException()
    {
        // Arrange
        var formId = -1;
        bool? isDraft = true;
        string? jsonData = null;

        // Act
        Action act = () => new PartialUpdateActiveFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(formId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        bool? isDraft = true;
        string? jsonData = SampleData.FORM_DEFINITION_JSON_DATA_1;

        // Act
        var command = new PartialUpdateActiveFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().Be(isDraft);
        command.JsonData.Should().Be(jsonData);
    }

    [Fact]
    public void Constructor_NullOptionalParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var formId = 1;
        bool? isDraft = null;
        string? jsonData = null;

        // Act
        var command = new PartialUpdateActiveFormDefinitionCommand(formId, isDraft, jsonData);

        // Assert
        command.FormId.Should().Be(formId);
        command.IsDraft.Should().BeNull();
        command.JsonData.Should().BeNull();
    }
}
