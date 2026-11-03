using System.Text.Json;
using Endatix.Modules.Audience.Endpoints.Audience.Variables;

namespace Endatix.Modules.Audience.Tests.Endpoints.Audience.Variables;

public sealed class SaveTestParticipantsValidatorTests
{
    private readonly SaveTestParticipantsValidator _validator = new();

    [Fact]
    public void Validate_PresetRepeatsAVariableName_Fails()
    {
        // Arrange
        const string body = """{"formId":1,"presets":[{"name":"A","variables":{"x":1,"x":2}}]}""";
        var request = JsonSerializer.Deserialize<SaveTestParticipantsRequest>(body, JsonSerializerOptions.Web)!;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorMessage == "Each variable can appear once in a preset.");
    }

    [Fact]
    public void Validate_PresetWithDistinctVariableNames_Passes()
    {
        // Arrange
        const string body = """{"formId":1,"presets":[{"name":"A","variables":{"x":1,"y":2}}]}""";
        var request = JsonSerializer.Deserialize<SaveTestParticipantsRequest>(body, JsonSerializerOptions.Web)!;

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
