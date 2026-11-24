using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public sealed class LegacyMatrixContributionTests
{
    private const string INTRO = """{"elements":[{"name":"intro","type":"text"}]}""";
    private const string NUMERIC_A = """[{"value":10,"text":"A"}]""";
    private const string STRINGS_AB = """["A","B"]""";

    [Theory]
    [InlineData(STRINGS_AB)]
    [InlineData("""[{"value":20,"text":"A"},"B"]""")]
    public void Merge_PartialWitnessContradictsCompleteBook_RefusesPromotion(string later)
    {
        // ID 1 was already published as number 10, under the legacy label A.
        var compile = () => Recover([Matrix(NUMERIC_A), Matrix(later)], Unmarked(STRINGS_AB));
        compile.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    [Fact]
    public void Merge_CompatiblePartialNumericWitness_KeepsItsCode()
    {
        var compiled = Recover([Matrix(NUMERIC_A), Matrix("""[{"value":10,"text":"A"},"B"]""")], Unmarked(STRINGS_AB));
        Columns(compiled).Should().Contain(("10", 1)).And.Contain(("B", 2));
    }

    [Fact]
    public void Merge_AbsenceBeforeIntroduction_ReplaysFromFirstAppearance()
    {
        var revised = Matrix("""["C","B"]""");
        var stored = Extend(Unmarked(STRINGS_AB), revised);
        var compiled = Recover([INTRO, Matrix(STRINGS_AB), revised], stored);
        Columns(compiled).Should().Contain([("A", 1), ("B", 2), ("C", 3)]);
    }

    [Theory]
    [InlineData("unreadable")]
    [InlineData("""{"pages":[{"elements":"unreadable"}]}""")]
    [InlineData("""{"elements":[{"type":"text","name":"m"}]}""")]
    [InlineData("""{"elements":[{"type":"text","name":"alias","valueName":"m"}]}""")]
    public void Merge_UnreadableOrOccupiedIntroduction_RefusesPromotion(string prefix)
    {
        var compile = () => Recover([prefix, Matrix(STRINGS_AB)], Unmarked(STRINGS_AB));
        compile.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    [Fact]
    public void Merge_MatrixAbsentAfterFirstAppearance_RefusesReintroduction()
    {
        var compile = () => Recover([Matrix(STRINGS_AB), INTRO, Matrix(STRINGS_AB)], Unmarked(STRINGS_AB));
        compile.Should().Throw<ReportingSchemaAliasConflictException>();
    }

    private static string Matrix(string columns) =>
        $$"""{"elements":[{"name":"m","type":"matrix","rows":["r"],"columns":{{columns}}}]}""";

    private static PersistedSchemaJson Unmarked(string choices)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(Matrix(choices));
        var book = JsonNode.Parse(compiled.CodebookJson)!;
        book["questions"]!["m"]!.AsObject().Remove("typedMatrixCodes");
        return new(compiled.FlatteningMapJson, book.ToJsonString());
    }

    private static PersistedSchemaJson Extend(PersistedSchemaJson stored, string next)
    {
        var merged = new FormSchemaCompiler().CompilePersisted(next, stored.FlatteningMap, stored.Codebook);
        return new(merged.FlatteningMapJson, merged.CodebookJson);
    }

    private static FormSchemaCompileResult Recover(string[] sources, PersistedSchemaJson stored) =>
        new FormSchemaCompiler().CompileLegacyMerge(Matrix("[80]"), stored,
            new LegacyMatrixHistory(FormSchemaFlatteningMap.FromJson(stored.FlatteningMap), sources));

    private static (string, int)[] Columns(FormSchemaCompileResult compiled)
    {
        using var doc = JsonDocument.Parse(compiled.CodebookJson);
        var columns = doc.RootElement.GetProperty("questions").GetProperty("m").GetProperty("columns");
        return [.. columns.EnumerateArray().Select(c => (c.GetProperty("value").GetString()!, c.GetProperty("id").GetInt32()))];
    }
}
