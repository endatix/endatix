using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class MatrixOrdinalScopeTests
{
    private static string Definition(string rows) => $$"""
        {"elements":[{"type":"matrix","name":"used","rows":["r"],"columns":["yes","no"]},
                     {"type":"matrix","name":"historical","rows":{{rows}},"columns":["a","b"]},
                     {"type":"dropdown","name":"pick","choices":["a","b"]}]}
        """;

    private static FormSchemaCompileResult MergeWithGap(string rows)
    {
        FormSchemaCompiler compiler = new();
        var stored = compiler.CompilePersisted(Definition("[]"));
        var book = JsonNode.Parse(stored.CodebookJson)!;
        book["questions"]!["historical"]!["columns"]![1]!["id"] = 7;
        book["choiceCatalogs"]!["pick"]!["choices"]![1]!["id"] = 9;
        return compiler.CompilePersisted(Definition(rows), stored.FlatteningMapJson, book.ToJsonString());
    }

    [Fact]
    public void Merge_OnlyUnusedMatrixAndDropdownHaveSparseIds_PreservesTheirIds()
    {
        var merged = MergeWithGap("[]");
        var book = JsonNode.Parse(merged.CodebookJson)!;
        ((int)book["questions"]!["historical"]!["columns"]![1]!["id"]!).Should().Be(7);
        ((int)book["choiceCatalogs"]!["pick"]!["choices"]![1]!["id"]!).Should().Be(9);
        merged.FlatteningMap.Columns.Single(c => c.Key == "used__r").MatrixColumnChoices.Should().Equal("yes", "no");
    }

    [Fact]
    public void Merge_NewRowWouldUseSparseOrdinalMap_RefusesToMiscodeAnswers()
    {
        var compile = () => MergeWithGap("""["new"]""");
        compile.Should().Throw<ReportingSchemaAliasConflictException>().Which.Detail.Should().Contain("historical");
    }
}
