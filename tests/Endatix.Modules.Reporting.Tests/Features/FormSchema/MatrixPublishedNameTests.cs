using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;
using Endatix.Modules.Reporting.Tests.Features.Export.Tabular;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixPublishedNameTests
{
    [Fact]
    public async Task Merge_ReusedDisplayLabel_PreservesPublishedNamesAndIdsInTheMatchingFiles()
    {
        var original = Definition("""[{"value":10,"text":"High"}]""");
        var revised = Definition("""[{"value":20,"text":"High"}]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        FixtureResponse[] responses = [Answer("old", "10"), Answer("new", "20")];
        pipeline.Flatten(original, responses[0]);
        var before = await ReadAsync(pipeline, pipeline.ExportRows(responses[..1]));
        before.Names.Should().BeEquivalentTo(new Dictionary<string, string> { ["1"] = "High" });

        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, responses[1]);
        var after = await ReadAsync(pipeline, pipeline.ExportRows(responses));
        after.Native.Should().Equal("10", "20");
        after.Ids.Should().Equal("1", "2");
        after.Decoded.Should().Equal("High", "High (20)");

        var later = Definition("""[{"value":30,"text":"High (20)"},{"value":20.0,"text":"Updated"}]""");
        pipeline.Recompile(later, FormSchemaCompileMode.Merge);
        var third = await ReadAsync(pipeline, pipeline.ExportRows(responses));
        third.Names["3"].Should().Be("High (20) (30)");
        third.Decoded.Should().Equal(after.Decoded);
        third.Native.Should().Equal(after.Native);
    }

    [Theory]
    [InlineData("\"na\"", "\"na\"", "na")]
    [InlineData("\"80\"", "\"80\"", "Quoted eighty")]
    public async Task Merge_ExactFlattenedStringWithoutSource_DecodesOnlyTheStringCategory(string answer, string native, string label)
    {
        var original = Definition("[80]");
        var revised = Definition("""[80,"na",{"value":"80","text":"Quoted eighty"}]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        FixtureResponse[] responses = [Answer("old", "80"), Answer("new", answer)];
        pipeline.Flatten(original, responses[0]);
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, responses[1]);

        var rows = pipeline.ExportRows(responses).Select(r => r with { SourceAnswersJson = null }).ToList();
        var files = await ReadAsync(pipeline, rows);

        files.Native.Should().Equal("80", native);
        files.Decoded.Should().Equal("80", label);
        files.Ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Merge_LiteralNameAlreadyUsesTheValueSuffix_LeavesBothPublishedNamesAlone()
    {
        var original = Definition("""[{"value":10,"text":"High"},{"value":30,"text":"High (20)"}]""");
        var revised = Definition("""[{"value":20,"text":"High"}]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        var response = Answer("new", "20");
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, response);

        var files = await ReadAsync(pipeline, pipeline.ExportRows([response]));

        files.Names["1"].Should().Be("High");
        files.Names["2"].Should().Be("High (20)");
        files.Decoded.Should().Equal("High (20) (2)");
    }

    private static string Definition(string columns) =>
        $$"""{"elements":[{"type":"matrix","name":"m","rows":["r"],"columns":{{columns}}}]}""";

    [Fact]
    public async Task Merge_StringsThenNumbers_OldAndNewDownloadsRetainAliasesAndCategoryMeanings()
    {
        // Arrange
        var original = Definition("""["first","second"]""");
        var revised = Definition("""["added","second"]""");
        var later = Definition("""[80,"added","second"]""");
        var pipeline = PersonalizationPipeline.Compile(original);
        FixtureResponse[] responses = [Answer("old", "\"first\""), Answer("new", "\"added\""), Answer("number", "80")];
        pipeline.Flatten(original, responses[0]);
        var first = await ReadAsync(pipeline, pipeline.ExportRows(responses[..1]));

        // Act
        pipeline.Recompile(revised, FormSchemaCompileMode.Merge);
        pipeline.Flatten(revised, responses[1]);
        var second = await ReadAsync(pipeline, pipeline.ExportRows(responses[..2]));
        pipeline.Recompile(later, FormSchemaCompileMode.Merge);
        pipeline.Flatten(later, responses[2]);
        var third = await ReadAsync(pipeline, pipeline.ExportRows(responses));

        // Assert
        first.Decoded.Should().Equal("first");
        second.Decoded.Should().Equal("first", "added");
        third.Native.Should().Equal("1", "3", "80");
        third.Ids.Should().Equal("1", "3", "4");
        third.Decoded.Should().Equal("first", "added", "80");
        third.Names["2"].Should().Be("second");
    }

    [Fact]
    public void Export_UnmarkedStringBook_KeepsLegacyShojiNames()
    {
        // Arrange
        var pipeline = PersonalizationPipeline.Compile(Definition("""["second"," second "]"""));
        var unmarked = JsonNode.Parse(pipeline.Schema.CodebookJson)!;
        unmarked["questions"]!["m"]!.AsObject().Remove("typedMatrixCodes");

        // Act
        var shoji = ShojiCodebookGenerator.Generate(pipeline.Schema.FlatteningMapJson, unmarked.ToJsonString(),
            ExportFormatSettings.InterimCrunchKeySeparator);

        // Assert
        using var book = JsonDocument.Parse(shoji);
        var table = book.RootElement.GetProperty("body").GetProperty("table");
        table.GetProperty("hidden").EnumerateArray().Select(a => a.GetString()).Should().Contain("FormId");
        var matrix = table.GetProperty("metadata").GetProperty("m");
        matrix.GetProperty("categories").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Equal("second", "second");
    }

    private static FixtureResponse Answer(string id, string json) => new(id, """{"m":{"r":""" + json + "}}", null);

    private sealed record Readback(string[] Native, Dictionary<string, string> Names, string[] Ids)
    {
        public IEnumerable<string> Decoded => Ids.Select(id => Names.GetValueOrDefault(id, "<missing>"));
    }

    private static async Task<Readback> ReadAsync(PersonalizationPipeline pipeline, IReadOnlyList<FlattenedExportRow> rows)
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = pipeline.Entity();
        var native = await SubmissionFileExport.ExportAsync(new("json", schema, rows, DefaultExportFormats.Json.SettingsJson), ct);
        using var data = JsonDocument.Parse(native);
        using var book = JsonDocument.Parse(await CrunchExport.ShojiCodebookAsync(schema, ct));
        var matrix = book.RootElement.GetProperty("body").GetProperty("table").GetProperty("metadata").GetProperty("m");
        var alias = matrix.GetProperty("subvariables")[0].GetProperty("alias").GetString()!;
        alias.Should().Be("m--r");
        var (header, records) = CrunchImportCheck.Read(await CrunchExport.CsvAsync(schema, rows, ct));
        return new(
            [.. data.RootElement.EnumerateArray().Select(r => r.GetProperty("m__r").GetRawText())],
            matrix.GetProperty("categories").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetRawText(), c => c.GetProperty("name").GetString()!),
            [.. records.Select(r => r[header.IndexOf(alias)])]);
    }
}
