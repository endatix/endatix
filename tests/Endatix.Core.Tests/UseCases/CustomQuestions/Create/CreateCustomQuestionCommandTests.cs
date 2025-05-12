using Endatix.Core.UseCases.CustomQuestions.Create;
using FluentAssertions;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.CustomQuestions.Create;

public class CreateCustomQuestionCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var jsonData = "{ \"type\": \"text\" }";
        var description = "Test Description";

        // Act
        Action act = () => new CreateCustomQuestionCommand(name, jsonData, description);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(name), Empty));
    }

    [Fact]
    public void Constructor_NullOrWhiteSpaceJsonData_ThrowsArgumentException()
    {
        // Arrange
        var name = "Test Question";
        var jsonData = "";
        var description = "Test Description";

        // Act
        Action act = () => new CreateCustomQuestionCommand(name, jsonData, description);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(jsonData), Empty));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Test Question";
        var jsonData = "{ \"type\": \"text\" }";
        var description = "Test Description";

        // Act
        var command = new CreateCustomQuestionCommand(name, jsonData, description);

        // Assert
        command.Name.Should().Be(name);
        command.JsonData.Should().Be(jsonData);
        command.Description.Should().Be(description);
    }

    [Fact]
    public void Constructor_ValidParametersWithoutDescription_SetsPropertiesCorrectly()
    {
        // Arrange
        var name = "Test Question";
        var jsonData = "{ \"type\": \"text\" }";

        // Act
        var command = new CreateCustomQuestionCommand(name, jsonData);

        // Assert
        command.Name.Should().Be(name);
        command.JsonData.Should().Be(jsonData);
        command.Description.Should().BeNull();
    }
} 