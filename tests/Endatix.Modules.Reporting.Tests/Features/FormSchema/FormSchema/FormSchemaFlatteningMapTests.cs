using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

public class FormSchemaFlatteningMapTests
{
    [Fact]
    public void FromJson_WithCurrentVersion_ParsesColumns()
    {
        const string json = """{"version":1,"columns":[{"key":"q1","kind":"Simple","label":"Q1","dataType":"string"}]}""";

        MergedFormSchema schema = FormSchemaFlatteningMap.FromJson(json);

        schema.Columns.Should().ContainSingle(column => column.Key == "q1");
    }

    [Theory]
    [InlineData("""[{"key":"q1"}]""")]
    [InlineData("""{"columns":[{"key":"q1"}]}""")]
    [InlineData("""{"version":2,"columns":[{"key":"q1"}]}""")]
    public void FromJson_WithUnsupportedShape_ThrowsJsonException(string json)
    {
        Action act = () => FormSchemaFlatteningMap.FromJson(json);

        act.Should().Throw<JsonException>();
    }
}
