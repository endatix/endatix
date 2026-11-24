using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
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
        var (header, records) = CrunchImportCheck.Read(await CrunchExport.CsvAsync(schema, rows, ct));
        return new(
            [.. data.RootElement.EnumerateArray().Select(r => r.GetProperty("m__r").GetRawText())],
            matrix.GetProperty("categories").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetRawText(), c => c.GetProperty("name").GetString()!),
            [.. records.Select(r => r[header.IndexOf(alias)])]);
    }
}
