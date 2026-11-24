using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

public sealed class IndexedPanelCommentTests
{
    [Theory]
    [InlineData(null, "First person", "Second person")]
    [InlineData("members", "First member", "Second member")]
    [InlineData("absent", null, null)]
    public void Flatten_IndexedPanel_UsesOnlyItsSubmittedAnswerKey(string? key, string? first, string? second)
    {
        // Arrange
        var definition = WithPanelKey(key);
        const string data = """
            {"household":[{"pick":{"value":"yes","comment":"First person"}},{"pick":{"value":"yes","comment":"Second person"}}],
             "members":[{"pick":{"value":"yes","comment":"First member"}},{"pick":{"value":"yes","comment":"Second member"}}]}
            """;
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        pipeline.Flatten(definition, new FixtureResponse("row", data, null));

        // Assert
        (pipeline.Rows["row"]["household__0__pick__yes__comment"]?.GetString()).Should().Be(first);
        (pipeline.Rows["row"]["household__1__pick__yes__comment"]?.GetString()).Should().Be(second);
    }

    [Fact]
    public void Flatten_SharedPanelStorageKey_LeavesCommentUnattributed()
    {
        // Arrange
        var definition = WithPanelKey("members", """{"type":"text","name":"members"}""");
        var pipeline = PersonalizationPipeline.Compile(definition);

        // Act
        pipeline.Flatten(definition, new FixtureResponse("row", """{"members":[{"pick":{"value":"yes","comment":"Ambiguous"}}]}""", null));

        // Assert
        pipeline.Rows["row"]["household__0__pick__yes__comment"].Should().BeNull();
    }

    private static string WithPanelKey(string? key, string? sibling = null)
    {
        var fixture = File.ReadAllText(Path.Combine(ScenarioGoldens.OutputRoot, "..", "PanelChoiceComments", "definition.json"));
        var definition = JsonNode.Parse(fixture)!;
        var elements = definition["pages"]![0]!["elements"]!.AsArray();
        if (key is not null)
        {
            elements[0]!["valueName"] = key;
        }

        if (sibling is not null)
        {
            elements.Add(JsonNode.Parse(sibling));
        }

        return definition.ToJsonString();
    }
}
