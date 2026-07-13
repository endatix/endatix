using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public class FlattenedSubmissionFlattenerTests
{
    [Fact]
    public void Flatten_SimpleSubmission_MapsDirectProperties()
    {
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition("simple-definition.json");
        JsonElement submission = FormSchemaFixtureLoader.LoadJson("simple-submission.json");
        JsonElement expected = FormSchemaFixtureLoader.LoadJson("simple-expected-flat.json");

        MergedFormSchema formSchema = new(FormDefinitionFlattener.Flatten(definition));
        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(submission, formSchema);

        foreach (JsonProperty property in expected.EnumerateObject())
        {
            flattened[property.Name]!.Value.GetRawText().Should().Be(property.Value.GetRawText());
        }
    }

    [Theory]
    [InlineData("checkbox-definition.json", "checkbox-submission.json", "checkbox-expected-flat.json")]
    [InlineData("paneldynamic-definition.json", "paneldynamic-submission.json", "paneldynamic-expected-flat.json")]
    [InlineData("nested-loop-definition.json", "nested-loop-submission.json", "nested-loop-expected-flat.json")]
    [InlineData("ranking-definition.json", "ranking-submission.json", "ranking-expected-flat.json")]
    [InlineData("multipletext-definition.json", "multipletext-submission.json", "multipletext-expected-flat.json")]
    [InlineData("matrixdropdown-definition.json", "matrixdropdown-submission.json", "matrixdropdown-expected-flat.json")]
    [InlineData("matrixdynamic-definition.json", "matrixdynamic-submission.json", "matrixdynamic-expected-flat.json")]
    public void Flatten_MapsSubmissionToFormSchemaKeys(
        string definitionFixture,
        string submissionFixture,
        string expectedFlatFixture)
    {
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition(definitionFixture);
        SchemaCompilationLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
            ? new SchemaCompilationLimits { MaxPanelCount = 2 }
            : definitionFixture.Contains("matrixdynamic", StringComparison.Ordinal)
                ? new SchemaCompilationLimits { MaxMatrixRowCount = 2 }
                : SchemaCompilationLimits.Default;

        MergedFormSchema formSchema = new(FormDefinitionFlattener.Flatten(definition, limits));
        JsonElement submission = FormSchemaFixtureLoader.LoadJson(submissionFixture);
        JsonElement expected = FormSchemaFixtureLoader.LoadJson(expectedFlatFixture);

        Dictionary<string, JsonElement?> flattened =
            FlattenedSubmissionFlattener.Flatten(submission, formSchema);

        foreach (JsonProperty property in expected.EnumerateObject())
        {
            flattened.Should().ContainKey(property.Name);
            JsonElement? actual = flattened[property.Name];
            actual.Should().NotBeNull();
            JsonElementsEqual(property.Value, actual!.Value).Should().BeTrue(
                $"expected {property.Name} to equal {property.Value}");
        }
    }

    private static bool JsonElementsEqual(JsonElement left, JsonElement right) =>
        left.GetRawText() == right.GetRawText();

    [Fact]
    public void ToJson_WritesPropertiesInSchemaColumnOrder()
    {
        MergedFormSchema formSchema = new(
        [
            new FormSchemaColumn("zebra", FormSchemaColumnKind.Simple, "zebra", "string"),
            new FormSchemaColumn("alpha", FormSchemaColumnKind.Simple, "alpha", "string"),
            new FormSchemaColumn("middle", FormSchemaColumnKind.Simple, "middle", "string"),
        ]);

        Dictionary<string, JsonElement?> flattened = new(StringComparer.Ordinal)
        {
            ["middle"] = JsonDocument.Parse("\"m\"").RootElement.Clone(),
            ["zebra"] = JsonDocument.Parse("\"z\"").RootElement.Clone(),
            ["alpha"] = JsonDocument.Parse("\"a\"").RootElement.Clone(),
        };

        string json = FlattenedSubmissionFlattener.ToJson(formSchema, flattened);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().Equal(["zebra", "alpha", "middle"]);
    }

    [Fact]
    public void Flatten_MalformedSubmissionRoot_DoesNotThrow()
    {
        MergedFormSchema formSchema = new(
        [
            new FormSchemaColumn("name", FormSchemaColumnKind.Simple, "name", "string"),
            new FormSchemaColumn("colors__red", FormSchemaColumnKind.ChoiceIndicator, "Red", "number",
                SourceQuestion: "colors", ChoiceValue: "red"),
            new FormSchemaColumn("rank__a", FormSchemaColumnKind.RankingChoice, "A", "number",
                SourceQuestion: "rank", ChoiceValue: "a"),
        ]);

        using JsonDocument document = JsonDocument.Parse("\"not-an-object\"");
        Dictionary<string, JsonElement?> flattened =
            FlattenedSubmissionFlattener.Flatten(document.RootElement, formSchema);

        flattened["name"].Should().BeNull();
        flattened["colors__red"]!.Value.GetRawText().Should().Be("0");
        flattened["rank__a"]!.Value.GetRawText().Should().Be("0");
    }

    [Fact]
    public void Flatten_NumericCheckboxAndRankingChoices_MatchCompiledKeys()
    {
        const string definition = """
            {
              "pages": [{
                "elements": [
                  {
                    "type": "checkbox",
                    "name": "payment",
                    "choices": [
                      { "value": 1, "text": "Cash" },
                      { "value": 2, "text": "Card" }
                    ]
                  },
                  {
                    "type": "ranking",
                    "name": "priority",
                    "choices": [
                      { "value": 10, "text": "Price" },
                      { "value": 20, "text": "Quality" }
                    ]
                  }
                ]
              }]
            }
            """;
        const string submission = """
            {
              "payment": [1],
              "priority": [20, 10]
            }
            """;

        using JsonDocument definitionDocument = JsonDocument.Parse(definition);
        using JsonDocument submissionDocument = JsonDocument.Parse(submission);

        MergedFormSchema formSchema = new(
            FormDefinitionFlattener.Flatten(definitionDocument.RootElement.Clone()));
        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(
            submissionDocument.RootElement.Clone(),
            formSchema);

        flattened["payment__1"]!.Value.GetRawText().Should().Be("1");
        flattened["payment__2"]!.Value.GetRawText().Should().Be("0");
        flattened["priority__10"]!.Value.GetRawText().Should().Be("2");
        flattened["priority__20"]!.Value.GetRawText().Should().Be("1");
    }

    [Fact]
    public void Flatten_LoopSourceFileUpload_ResolvesNestedFileValue()
    {
        const string definitionJson = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "checkbox",
                      "name": "brands",
                      "choices": [
                        { "value": "nike", "text": "Nike" }
                      ]
                    },
                    {
                      "type": "paneldynamic",
                      "name": "brandLoop",
                      "loopSource": ["brands"],
                      "templateElements": [
                        {
                          "type": "file",
                          "name": "brandPhoto",
                          "title": "Brand photo"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        const string submissionJson = """
            {
              "brands": ["nike"],
              "brandLoop": [
                {
                  "itemValue": "nike",
                  "brandPhoto": [
                    { "content": "nike-photo.jpg" }
                  ]
                }
              ]
            }
            """;

        using JsonDocument definitionDocument = JsonDocument.Parse(definitionJson);
        using JsonDocument submissionDocument = JsonDocument.Parse(submissionJson);
        MergedFormSchema formSchema = new(
            FormDefinitionFlattener.Flatten(definitionDocument.RootElement.Clone()));
        Dictionary<string, JsonElement?> flattened = FlattenedSubmissionFlattener.Flatten(
            submissionDocument.RootElement.Clone(),
            formSchema);

        flattened["brandLoop__nike__brandPhoto"]!.Value.GetRawText().Should().Be("\"nike-photo.jpg\"");
    }
}
