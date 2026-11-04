using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Codebook;

/// <summary>
/// The kind-tagged choice catalog a compile writes for select questions, and how a merge keeps its ids.
/// </summary>
public sealed class ChoiceCatalogCodebookTests
{
    private const string WithSpecials = """
        {
          "storeOthersAsComment": false,
          "pages": [{ "name": "p1", "elements": [
            { "type": "radiogroup", "name": "rg", "choices": ["a", { "value": "b", "isExclusive": true }],
              "showOtherItem": true, "showNoneItem": true, "noneText": { "default": "Nothing", "es": "Nada" },
              "showRefuseItem": true, "showDontKnowItem": true },
            { "type": "checkbox", "name": "cb", "choices": ["x"], "showNoneItem": true },
            { "type": "ranking", "name": "rank", "choices": ["r1", "r2"] }
          ]}]
        }
        """;

    [Fact]
    public void CompilePersisted_SelectQuestion_WritesKindExclusiveMultipleAndOtherStorage()
    {
        var catalog = CatalogOf(new FormSchemaCompiler().CompilePersisted(WithSpecials).CodebookJson, "rg");

        catalog.GetProperty("multiple").GetBoolean().Should().BeFalse();
        catalog.GetProperty("otherStorage").GetString().Should().Be("value");
        Entries(catalog).Should().Equal(
            """{"value":"a","id":1,"text":{"default":"a"}}""",
            """{"value":"b","id":2,"exclusive":true,"text":{"default":"b"}}""",
            """{"value":"other","id":3,"kind":"other","text":{"default":"Other"}}""",
            """{"value":"none","id":4,"kind":"none","exclusive":true,"text":{"default":"Nothing","es":"Nada"}}""",
            """{"value":"refused","id":5,"kind":"refused","exclusive":true,"text":{"default":"Refuse to answer"}}""",
            """{"value":"dontknow","id":6,"kind":"dontknow","exclusive":true,"text":{"default":"Don\u0027t know"}}""");
    }

    [Fact]
    public void CompilePersisted_QuestionEntryChoices_MatchTheCatalog()
    {
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(WithSpecials).CodebookJson);
        var root = codebook.RootElement;

        foreach (var question in new[] { "rg", "cb" })
        {
            Entries(root.GetProperty("questions").GetProperty(question)).Should().Equal(
                Entries(root.GetProperty("choiceCatalogs").GetProperty(question)));
        }
    }

    [Fact]
    public void CompilePersisted_CheckboxWithoutOther_IsMultipleWithoutOtherStorage()
    {
        var catalog = CatalogOf(new FormSchemaCompiler().CompilePersisted(WithSpecials).CodebookJson, "cb");

        catalog.GetProperty("multiple").GetBoolean().Should().BeTrue();
        catalog.TryGetProperty("otherStorage", out _).Should().BeFalse();
    }

    [Fact]
    public void CompilePersisted_Ranking_HasNoSelectFields()
    {
        var catalog = CatalogOf(new FormSchemaCompiler().CompilePersisted(WithSpecials).CodebookJson, "rank");

        catalog.TryGetProperty("multiple", out _).Should().BeFalse();
    }

    [Fact]
    public void CompilePersisted_SpecialIndicatorColumn_IsLabelledByTheItemText()
    {
        using var codebook = JsonDocument.Parse(new FormSchemaCompiler().CompilePersisted(WithSpecials).CodebookJson);

        var column = codebook.RootElement.GetProperty("columns").GetProperty("cb__none");

        column.GetProperty("choiceValue").GetString().Should().Be("none");
        column.GetProperty("choiceLabel").GetRawText().Should().Be("""{"default":"None"}""");
    }

    [Fact]
    public void CompilePersisted_MergeAddingSpecials_KeepsEveryOldIdAndAppendsTheSpecials()
    {
        // Arrange: v1 has no special items and was stored before this change; v2 removes "b" and adds specials.
        const string v1 = """
            {"pages":[{"elements":[{"type":"radiogroup","name":"rg","choices":["a","b","c"],"showOtherItem":true}]}]}
            """;
        const string v2 = """
            {"pages":[{"elements":[{"type":"radiogroup","name":"rg","choices":["a","c"],"showOtherItem":true,
              "storeOthersAsComment":false,"showNoneItem":true,"showRefuseItem":true}]}]}
            """;
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(v1);

        // Act
        var second = compiler.CompilePersisted(v2, first.FlatteningMapJson, first.CodebookJson, FormSchemaCompileMode.Merge);

        // Assert
        var catalog = CatalogOf(second.CodebookJson, "rg");
        Ids(catalog).Should().Equal(("a", 1), ("b", 2), ("c", 3), ("other", 4), ("none", 5), ("refused", 6));
        catalog.GetProperty("multiple").GetBoolean().Should().BeFalse();
        catalog.GetProperty("otherStorage").GetString().Should().Be("value");
        Entries(catalog)[4].Should().Be("""{"value":"none","id":5,"kind":"none","exclusive":true,"text":{"default":"None"}}""");
    }

    [Fact]
    public void CompilePersisted_MergeAfterValueStorageWasDropped_KeepsValueStorage()
    {
        // Rows saved under value storage keep their typed text in the value; a later comment-storage definition must
        // not make the flatten forget that.
        const string v1 = """{"pages":[{"elements":[{"type":"radiogroup","name":"rg","choices":["a"],"showOtherItem":true,"storeOthersAsComment":false}]}]}""";
        const string v2 = """{"pages":[{"elements":[{"type":"radiogroup","name":"rg","choices":["a"],"showOtherItem":true}]}]}""";
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(v1);

        var second = compiler.CompilePersisted(v2, first.FlatteningMapJson, first.CodebookJson, FormSchemaCompileMode.Merge);

        CatalogOf(second.CodebookJson, "rg").GetProperty("otherStorage").GetString().Should().Be("value");
    }

    [Fact]
    public void CompilePersisted_MergeAfterASelectBecameText_DropsTheSelectFields()
    {
        // The name now holds a text question: its answers are free text, never a select's.
        const string v1 = """{"pages":[{"elements":[{"type":"dropdown","name":"q","choices":["a"],"showOtherItem":true,"storeOthersAsComment":false}]}]}""";
        const string v2 = """{"pages":[{"elements":[{"type":"text","name":"q"}]}]}""";
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(v1);

        var second = compiler.CompilePersisted(v2, first.FlatteningMapJson, first.CodebookJson, FormSchemaCompileMode.Merge);

        var catalog = CatalogOf(second.CodebookJson, "q");
        catalog.TryGetProperty("multiple", out _).Should().BeFalse();
        catalog.TryGetProperty("otherStorage", out _).Should().BeFalse();
        Ids(catalog).Should().Equal(("a", 1), ("other", 2));
    }

    [Fact]
    public void CompilePersisted_MergeAfterASelectWasRemoved_KeepsItsSelectFields()
    {
        const string v1 = """{"pages":[{"elements":[{"type":"dropdown","name":"q","choices":["a"]},{"type":"text","name":"t"}]}]}""";
        const string v2 = """{"pages":[{"elements":[{"type":"text","name":"t"}]}]}""";
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(v1);

        var second = compiler.CompilePersisted(v2, first.FlatteningMapJson, first.CodebookJson, FormSchemaCompileMode.Merge);

        CatalogOf(second.CodebookJson, "q").GetProperty("multiple").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void CompilePersisted_MergeTwice_IsIdempotent()
    {
        FormSchemaCompiler compiler = new();
        var first = compiler.CompilePersisted(WithSpecials);

        var second = compiler.CompilePersisted(WithSpecials, first.FlatteningMapJson, first.CodebookJson, FormSchemaCompileMode.Merge);
        var third = compiler.CompilePersisted(WithSpecials, second.FlatteningMapJson, second.CodebookJson, FormSchemaCompileMode.Merge);

        second.CodebookJson.Should().Be(first.CodebookJson);
        third.CodebookJson.Should().Be(first.CodebookJson);
    }

    private static JsonElement CatalogOf(string codebookJson, string question)
    {
        using var codebook = JsonDocument.Parse(codebookJson);
        return codebook.RootElement.GetProperty("choiceCatalogs").GetProperty(question).Clone();
    }

    private static List<string> Entries(JsonElement owner) =>
        owner.GetProperty("choices").EnumerateArray().Select(choice => choice.GetRawText()).ToList();

    private static List<(string, int)> Ids(JsonElement catalog) =>
        catalog.GetProperty("choices").EnumerateArray()
            .Select(choice => (choice.GetProperty("value").GetString()!, choice.GetProperty("id").GetInt32()))
            .ToList();
}
