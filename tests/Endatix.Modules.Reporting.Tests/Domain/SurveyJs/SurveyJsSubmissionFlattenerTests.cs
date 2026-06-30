using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Domain.SurveyJs;

public class SurveyJsSubmissionFlattenerTests
{
    [Fact]
    public void Flatten_SimpleSubmission_MapsDirectProperties()
    {
        JsonElement definition = SurveyJsFixtureLoader.LoadDefinition("simple-definition.json");
        JsonElement submission = SurveyJsFixtureLoader.LoadJson("simple-submission.json");
        JsonElement expected = SurveyJsFixtureLoader.LoadJson("simple-expected-flat.json");

        MergedCodebook codebook = new(SurveyJsDefinitionFlattener.Flatten(definition));
        Dictionary<string, JsonElement?> flattened = SurveyJsSubmissionFlattener.Flatten(submission, codebook);

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
    public void Flatten_MapsSubmissionToCodebookKeys(
        string definitionFixture,
        string submissionFixture,
        string expectedFlatFixture)
    {
        JsonElement definition = SurveyJsFixtureLoader.LoadDefinition(definitionFixture);
        FlatteningLimits limits = definitionFixture.Contains("paneldynamic", StringComparison.Ordinal)
            ? new FlatteningLimits { MaxPanelCount = 2 }
            : FlatteningLimits.Default;

        MergedCodebook codebook = new(SurveyJsDefinitionFlattener.Flatten(definition, limits));
        JsonElement submission = SurveyJsFixtureLoader.LoadJson(submissionFixture);
        JsonElement expected = SurveyJsFixtureLoader.LoadJson(expectedFlatFixture);

        Dictionary<string, JsonElement?> flattened =
            SurveyJsSubmissionFlattener.Flatten(submission, codebook);

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
