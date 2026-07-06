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
    public void Flatten_MapsSubmissionToFormSchemaKeys(
        string definitionFixture,
        string submissionFixture,
        string expectedFlatFixture)
    {
        JsonElement definition = FormSchemaFixtureLoader.LoadDefinition(definitionFixture);
        SchemaCompilationLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
            ? new SchemaCompilationLimits { MaxPanelCount = 2 }
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
}
