using System.Text.Json;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

public sealed class ChoiceCommentHistoryTests
{
    private const string RADIO = """{"type":"radiogroup","name":"q","choices":[{"value":"yes","showCommentArea":true}]}""";
    private const string TAG = """{"type":"tagbox","name":"q","choices":[{"value":"yes","showCommentArea":true}]}""";
    private const string MULTIPLE_TEXT = """{"type":"multipletext","name":"q","items":[{"name":"value"},{"name":"comment"}]}""";
    private const string NOTE = """{"q":{"value":"yes","comment":"Stored text"}}""";
    private const string NOTES = """{"q":[{"value":"yes","comment":"Stored text"}]}""";

    [Theory]
    [InlineData(RADIO, NOTE, "Stored text")]
    [InlineData(MULTIPLE_TEXT, NOTE, null)]
    [InlineData(RADIO, NOTES, null)]
    [InlineData(TAG, NOTE, null)]
    [InlineData(TAG, NOTES, "Stored text")]
    [InlineData(TAG, """{"q":["yes"]}""", null)]
    [InlineData(null, NOTE, null)]
    [InlineData("""{"type":"dropdown","name":"q","valueName":"answer","choices":[{"value":"yes","showCommentArea":true}]}""",
        """{"answer":{"value":"yes","comment":"Real note"},"q":{"value":"yes","comment":"Wrong key"}}""", "Real note")]
    [InlineData("""{"type":"dropdown","name":"q","valueName":"answer","choices":[{"value":"yes","showCommentArea":true}]},{"type":"text","name":"answer"}""",
        """{"answer":{"value":"yes","comment":"Ambiguous"}}""", null)]
    [InlineData("""{"type":"checkbox","name":"q","valuePropertyName":"brand","choices":[{"value":"yes","showCommentArea":true}]}""", NOTES, null)]
    [InlineData("""{"type":"radiogroup","name":"q","choices":["yes"]}""", NOTE, null)]
    public void Flatten_RetainedComment_UsesTheSubmittedQuestionAndItsStorageShape(string? submitted, string raw, string? text)
    {
        // Arrange
        FormSchemaCompiler compiler = new();
        var original = compiler.CompilePersisted(Definition(RADIO));
        var merged = compiler.CompilePersisted(Definition(MULTIPLE_TEXT), original.FlatteningMapJson, original.CodebookJson);
        var catalogs = ChoiceCatalogs.Read(merged.CodebookJson, true, submitted is null ? null : Definition(submitted));
        using var answer = JsonDocument.Parse(raw);

        // Act
        var row = FlattenedSubmissionFlattener.Flatten(answer.RootElement, merged.FlatteningMap, catalogs);

        // Assert
        row["q__yes__comment"]?.GetString().Should().Be(text);
    }

    private static string Definition(string question) => $$"""{"pages":[{"elements":[{{question}}]}]}""";

    [Theory]
    [InlineData("[]")]
    [InlineData("unparseable")]
    public void HasColumnsFor_UnreadableHistoricalDefinition_RetainsTheExistingMap(string historical)
    {
        // Arrange
        FormSchemaCompiler compiler = new();
        var schema = compiler.CompilePersisted(Definition(RADIO));

        // Act
        var covered = compiler.HasColumnsFor(schema.FlatteningMapJson, historical);

        // Assert
        covered.Should().BeTrue();
    }
}
