using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.FlattenedFormDefinition;

public class FormDefinitionFlattenerTests
{
    [Theory]
    [InlineData("simple-definition.json", "simple-expected-keys.json")]
    [InlineData("checkbox-definition.json", "checkbox-expected-keys.json")]
    [InlineData("paneldynamic-definition.json", "paneldynamic-expected-keys.json")]
    [InlineData("nested-loop-definition.json", "nested-loop-expected-keys.json")]
    [InlineData("ranking-definition.json", "ranking-expected-keys.json")]
    [InlineData("multipletext-definition.json", "multipletext-expected-keys.json")]
    public void Flatten_ProducesExpectedKeys(string definitionFixture, string expectedKeysFixture)
    {
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition(definitionFixture);
        SchemaCompilationLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
            ? new SchemaCompilationLimits { MaxPanelCount = 2 }
            : SchemaCompilationLimits.Default;

        IReadOnlyList<FormSchemaColumn> columns =
            FormDefinitionFlattener.Flatten(definition, limits);

        columns.Select(column => column.Key).Should().BeEquivalentTo(
            FormSchemaFixtureLoader.LoadExpectedKeys(expectedKeysFixture),
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Flatten_SkipsNonDataElements()
    {
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("simple-definition.json");

        IReadOnlyList<FormSchemaColumn> columns =
            FormDefinitionFlattener.Flatten(definition);

        columns.Should().NotContain(column => column.Key == "info");
    }

    [Fact]
    public void Flatten_DuplicateColumnKey_Throws()
    {
        // Arrange
        const string json = """
            {
              "pages": [
                {
                  "elements": [
                    { "type": "text", "name": "score" }
                  ]
                }
              ],
              "calculatedValues": [
                { "name": "score", "expression": "1" }
              ]
            }
            """;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement definition = document.RootElement.Clone();

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(definition);

        // Assert
        SchemaCompilationLimitExceededException exception = act
            .Should().Throw<SchemaCompilationLimitExceededException>().Which;
        exception.LimitKind.Should().Be(SchemaCompilationLimitKind.DuplicateColumnKey);
        exception.Context.Should().Be("score");
    }

    [Fact]
    public void Flatten_ExceedsMaxNestingDepth_Throws()
    {
        // Arrange
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("paneldynamic-definition.json");
        SchemaCompilationLimits limits = new() { MaxNestingDepth = 0, MaxPanelCount = 2 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

        // Assert
        act.Should().Throw<SchemaCompilationLimitExceededException>()
            .Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxNestingDepth);
    }

    [Fact]
    public void Flatten_CapsSurveyMaxPanelCountByLimits()
    {
        // Arrange
        const string json = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "paneldynamic",
                      "name": "contacts",
                      "maxPanelCount": 50,
                      "templateElements": [
                        { "type": "text", "name": "email" }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement definition = document.RootElement.Clone();
        SchemaCompilationLimits limits = new() { MaxPanelCount = 2 };

        // Act
        IReadOnlyList<FormSchemaColumn> columns = FormDefinitionFlattener.Flatten(definition, limits);

        // Assert
        columns.Select(column => column.Key).Should().BeEquivalentTo(
        [
            "contacts__0__email",
            "contacts__1__email",
        ]);
    }

    [Fact]
    public void Flatten_ExceedsMaxChoicesPerQuestion_Matrix_Throws()
    {
        // Arrange
        const string json = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "matrix",
                      "name": "satisfaction",
                      "rows": ["r1", "r2", "r3"]
                    }
                  ]
                }
              ]
            }
            """;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement definition = document.RootElement.Clone();
        SchemaCompilationLimits limits = new() { MaxChoicesPerQuestion = 2 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

        // Assert
        SchemaCompilationLimitExceededException exception = act
            .Should().Throw<SchemaCompilationLimitExceededException>().Which;
        exception.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxChoicesPerQuestion);
        exception.Context.Should().Be("satisfaction");
    }

    [Fact]
    public void Flatten_ExceedsMaxLoopCombinations_Throws()
    {
        // Arrange
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("nested-loop-definition.json");
        SchemaCompilationLimits limits = new() { MaxLoopCombinations = 1 };

        // Act
        Action act = () => FormDefinitionFlattener.Flatten(definition, limits);

        // Assert
        act.Should().Throw<SchemaCompilationLimitExceededException>()
            .Which.LimitKind.Should().Be(SchemaCompilationLimitKind.MaxLoopCombinations);
    }
}
