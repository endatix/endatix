using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Domain.SurveyJs;

public class SurveyJsDefinitionFlattenerTests
{
    [Theory]
    [InlineData("simple-definition.json", "simple-expected-keys.json")]
    [InlineData("checkbox-definition.json", "checkbox-expected-keys.json")]
    [InlineData("paneldynamic-definition.json", "paneldynamic-expected-keys.json")]
    [InlineData("nested-loop-definition.json", "nested-loop-expected-keys.json")]
    [InlineData("ranking-definition.json", "ranking-expected-keys.json")]
    public void Flatten_ProducesExpectedKeys(string definitionFixture, string expectedKeysFixture)
    {
        JsonElement definition = SurveyJsFixtureLoader.LoadDefinition(definitionFixture);
        FlatteningLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
            ? new FlatteningLimits { MaxPanelCount = 2 }
            : FlatteningLimits.Default;

        IReadOnlyList<CodebookColumnDefinition> columns =
            SurveyJsDefinitionFlattener.Flatten(definition, limits);

        columns.Select(column => column.Key).Should().BeEquivalentTo(
            SurveyJsFixtureLoader.LoadExpectedKeys(expectedKeysFixture),
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Flatten_SkipsNonDataElements()
    {
        JsonElement definition = SurveyJsFixtureLoader.LoadDefinition("simple-definition.json");

        IReadOnlyList<CodebookColumnDefinition> columns =
            SurveyJsDefinitionFlattener.Flatten(definition);

        columns.Should().NotContain(column => column.Key == "info");
    }

    [Fact]
    public void Compile_AppendOnlyMerge_PreservesHistoricalKeys()
    {
        const string versionOne = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "firstName", "title": "First name" }
                  ]
                }
              ]
            }
            """;

        const string versionTwo = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "firstName", "title": "First name" },
                    { "type": "text", "name": "lastName", "title": "Last name" }
                  ]
                }
              ]
            }
            """;

        FormExportSchemaCompiler compiler = new();
        MergedCodebook firstPass = compiler.Compile(versionOne);
        MergedCodebook merged = compiler.Compile(versionTwo, firstPass);

        merged.Columns.Select(column => column.Key).Should().Equal("firstName", "lastName");
    }
}
