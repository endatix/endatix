using System.Text.Json;
using Endatix.Api.Endpoints.Forms;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class CreateFormValidatorTests
{
    private readonly CreateFormValidator _validator = new();

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static CreateFormRequest Request() => new() { Name = "Test Form", IsEnabled = true };

    [Fact]
    public void Validate_WithFormDefinitionSchemaObject_IsValid()
    {
        var request = Request();
        request.FormDefinitionSchema = Json("""{ "type": "object" }""");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOnlyObsoleteFormDefinitionJsonData_IsValid()
    {
#pragma warning disable CS0618 // Subject under test: compatibility fallback
        var request = Request();
        request.FormDefinitionJsonData = """{ "type": "object" }""";
#pragma warning restore CS0618

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNeitherFormDefinitionField_IsInvalid()
    {
        _validator.Validate(Request()).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public void Validate_WithNonObjectFormDefinitionSchema_IsInvalid(string json)
    {
        var request = Request();
        request.FormDefinitionSchema = Json(json);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
