using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>The placement rules <see cref="CrunchImportCheck"/> holds a codebook to, on hand-written codebooks.</summary>
public sealed class CrunchImportCheckTests
{
    private const string Csv = "FormId,Id,q1\n1,10,a\n";

    [Fact]
    public void Problems_EveryVariableOrderedInFoldersOrHidden_None()
    {
        // Arrange
        var shoji = Codebook("""[{"group":"Meta-data","entities":["Id"]},{"group":"Questions","entities":[{"group":"Panel","entities":["q1"]}]}]""", """["FormId"]""");

        // Act & Assert
        CrunchImportCheck.Problems(shoji, Csv).Should().BeEmpty();
    }

    [Fact]
    public void Problems_VariableNeitherOrderedNorHidden_ReportsIt()
    {
        // Arrange
        var shoji = Codebook("""[{"group":"Meta-data","entities":["Id"]}]""", """["FormId"]""");

        // Act & Assert
        CrunchImportCheck.Problems(shoji, Csv).Should().Equal("Shoji alias 'q1' is neither ordered nor hidden");
    }

    [Fact]
    public void Problems_OrderedAliasWithoutVariable_ReportsIt()
    {
        // Arrange
        var shoji = Codebook("""["Id","q1","q2"]""", """["FormId"]""");

        // Act & Assert
        CrunchImportCheck.Problems(shoji, Csv).Should().Equal("placed alias 'q2' has no Shoji variable");
    }

    [Fact]
    public void Problems_AliasInTwoFolders_ReportsItOrderedTwice()
    {
        // Arrange
        var shoji = Codebook("""[{"group":"A","entities":["Id","q1"]},{"group":"B","entities":[{"group":"C","entities":["q1"]}]}]""", """["FormId"]""");

        // Act & Assert
        CrunchImportCheck.Problems(shoji, Csv).Should().Equal("Shoji alias 'q1' is ordered more than once");
    }

    [Fact]
    public void Problems_AliasHiddenAndOrdered_ReportsIt()
    {
        // Arrange
        var shoji = Codebook("""["FormId","Id","q1"]""", """["FormId"]""");

        // Act & Assert
        CrunchImportCheck.Problems(shoji, Csv).Should().Equal("Shoji alias 'FormId' is both hidden and ordered");
    }

    private static string Codebook(string order, string hidden) => $$$$"""
        {"element":"shoji:entity","body":{"table":{"element":"crunch:table","metadata":{
          "FormId":{"type":"numeric","alias":"FormId","name":"Form ID"},
          "Id":{"type":"numeric","alias":"Id","name":"Submission ID"},
          "q1":{"type":"text","alias":"q1","name":"Q1"}
        },"order":{{{{order}}}},"hidden":{{{{hidden}}}}}}}
        """;
}
