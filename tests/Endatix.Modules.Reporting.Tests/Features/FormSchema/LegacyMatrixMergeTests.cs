using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class LegacyMatrixMergeTests
{
    private static string Stored(string file) => FormSchemaFixtureLoader.LoadText("LegacyMatrix/" + file + ".json");

    [Fact]
    public void Compile_ConfirmedLegacyDefinition_RepairsCodesAndPublishesAnUnrelatedQuestion()
    {
        var original = Stored("definition");
        var revised = JsonNode.Parse(original)!;
        revised["pages"]![0]!["elements"]!.AsArray().Add(JsonNode.Parse("""{"type":"text","name":"newField"}"""));

        var compiled = Merge(revised.ToJsonString(), [original]);

        using var book = JsonDocument.Parse(compiled.CodebookJson);
        var questions = book.RootElement.GetProperty("questions");
        questions.TryGetProperty("newField", out _).Should().BeTrue();
        var cols = questions.GetProperty("rating").GetProperty("columns").EnumerateArray().ToList();
        cols.Select(c => c.GetProperty("value").GetString()).Should().Equal("1", "2", "3", "4");
        cols.Select(c => c.GetProperty("id").GetInt32()).Should().Equal(1, 2, 3, 4);
        compiled.FlatteningMap.Columns.Select(c => c.Key).Should().Contain(["rating__a", "rating__b", "newField"]);
    }

    [Fact]
    public void Merge_ReorderedOrReplacedCodeWithSameLabel_UsesTheHistoricalCodeNotTodaysPosition()
    {
        var original = Stored("definition");
        var current = JsonNode.Parse(original)!;
        var columns = current["pages"]![0]!["elements"]![0]!["columns"]!.AsArray();
        columns[0]!["value"] = 20;
        var moved = columns[1]!;
        columns.RemoveAt(1);
        columns.Insert(0, moved);

        using var book = JsonDocument.Parse(Merge(current.ToJsonString(), [original]).CodebookJson);
        var ids = book.RootElement.GetProperty("questions").GetProperty("rating").GetProperty("columns").EnumerateArray()
            .ToDictionary(c => c.GetProperty("value").GetString()!, c => c.GetProperty("id").GetInt32());
        ids["1"].Should().Be(1);
        ids["2"].Should().Be(2);
        ids["20"].Should().Be(5);
    }

    [Fact]
    public void Merge_IndistinguishableHistoricalDefinitionsWithDifferentCodes_Refuses()
    {
        var original = Stored("definition");
        var alternative = JsonNode.Parse(original)!;
        alternative["pages"]![0]!["elements"]![0]!["columns"]![0]!["value"] = 20;

        var compile = () => Merge(original, [original, alternative.ToJsonString()]);

        compile.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    [Fact]
    public void Merge_PreviouslyOmittedPrimitive_DoesNotTakeAPublishedId()
    {
        var historical = JsonNode.Parse(Stored("definition"))!;
        historical["pages"]![0]!["elements"]![0]!["columns"]!.AsArray().Insert(0, JsonValue.Create(80));
        var original = historical.ToJsonString();
        var book = Merge(original, [original]).CodebookJson;

        using var document = JsonDocument.Parse(book);
        var columns = document.RootElement.GetProperty("questions").GetProperty("rating").GetProperty("columns");
        columns[0].GetProperty("value").GetString().Should().Be("80");
        columns.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).Should().Equal(5, 1, 2, 3, 4);
    }

    private static FormSchemaCompileResult Merge(string current, string[] history)
    {
        PersistedSchemaJson stored = new(Stored("map"), Stored("codebook"));
        return new FormSchemaCompiler().CompileLegacyMerge(current, stored,
            new LegacyMatrixHistory(FormSchemaFlatteningMap.FromJson(stored.FlatteningMap), history));
    }
}
