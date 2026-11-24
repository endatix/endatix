using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Choices;

public sealed class ChoiceCommentAliasTests
{
    private const string COMMENT = """{"value":"a","showCommentArea":true}""";

    [Theory]
    [InlineData("a__comment")]
    [InlineData("a--comment")]
    public void Compile_NewComment_KeepsOrdinaryKeysAndSkipsOccupiedSuffixes(string ordinary)
    {
        // Arrange
        FormSchemaCompiler compiler = new();
        var old = compiler.CompilePersisted(Definition($"\"{ordinary}\",\"a__comment_2\""));
        var next = Definition(COMMENT);

        // Act
        var merged = compiler.CompilePersisted(next, old.FlatteningMapJson, old.CodebookJson);
        var again = compiler.CompilePersisted(next, merged.FlatteningMapJson, merged.CodebookJson);
        var standalone = compiler.CompilePersisted(Definition($"{COMMENT},\"{ordinary}\",\"a__comment_2\""));

        // Assert
        foreach (var schema in new[] { merged, again, standalone })
        {
            var columns = schema.FlatteningMap.Columns;
            columns.Single(c => c.Key == $"q__{ordinary}").Kind.Should().Be(FormSchemaColumnKind.ChoiceIndicator);
            columns.Single(c => c.Kind == FormSchemaColumnKind.ChoiceComment).Key.Should().Be("q__a__comment_3");
        }
        compiler.HasColumnsFor(again.FlatteningMapJson, next).Should().BeTrue();
    }

    [Theory]
    [InlineData("a__comment")]
    [InlineData("a--comment")]
    public void Compile_OrdinaryChoiceWouldTakePublishedComment_RejectsMergeAndReplace(string ordinary)
    {
        // Arrange
        FormSchemaCompiler compiler = new();
        var old = compiler.CompilePersisted(Definition(COMMENT));
        var next = Definition(JsonSerializer.Serialize(ordinary));

        // Act
        foreach (var mode in new[] { FormSchemaCompileMode.Merge, FormSchemaCompileMode.Replace })
        {
            var compile = () => compiler.CompilePersisted(next, old.FlatteningMapJson, old.CodebookJson, mode);

            // Assert
            compile.Should().Throw<ReportingSchemaAliasConflictException>().Which.Detail.Should()
                .Contain("q--a--comment").And.Contain("ChoiceComment").And.Contain("ChoiceIndicator");
        }
    }

    private static string Definition(string choices) =>
        """{"pages":[{"elements":[{"type":"checkbox","name":"q","choices":[""" + choices + "]}]}]}";

    [Fact]
    public void Compile_CommentWouldTakeRankingArrayAlias_PreservesThePublishedNumericArray()
    {
        // Arrange
        const string ranking = """{"type":"ranking","name":"q--a--comment","choices":["first","second"]}""";
        var first = """{"pages":[{"elements":[""" + ranking + "]}]}";
        const string checkbox = """{"type":"checkbox","name":"q","choices":[{"value":"a","showCommentArea":true}]}""";
        var next = """{"pages":[{"elements":[""" + ranking + "," + checkbox + "]}]}";
        var pipeline = Personalization.PersonalizationPipeline.Compile(first);

        // Act
        pipeline.Recompile(next, FormSchemaCompileMode.Merge);
        using var shoji = JsonDocument.Parse(pipeline.ShojiCodebook());

        // Assert
        var metadata = shoji.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata");
        metadata.GetProperty("q--a--comment").GetProperty("type").GetString().Should().Be("numeric_array");
        metadata.GetProperty("q--a--comment_2").GetProperty("type").GetString().Should().Be("text");
    }
}
