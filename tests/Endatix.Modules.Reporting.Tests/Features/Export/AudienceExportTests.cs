using System.Text.Json;
using Endatix.Modules.Reporting.Features.Export;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class AudienceExportTests
{
    private const string Snapshot = """
        {"identifier":"CG-000020-01","variables":{"family_size":"3 to 4 people","city":"07. PACIFIC - PORTLAND","age":47}}
        """;

    [Fact]
    public void Columns_FromSnapshots_AddsIdentifierAndEachVariable()
    {
        IReadOnlyList<AudienceColumn> columns = AudienceExport.Columns([Snapshot], new HashSet<string>());

        columns.Select(column => column.ExportKey).Should().Equal(
            "identifier",
            "age",
            "city",
            "family_size");
    }

    [Fact]
    public void Columns_WhenNameIsAQuestion_PrefixesTheAudienceColumn()
    {
        IReadOnlyList<AudienceColumn> columns = AudienceExport.Columns(
            [Snapshot],
            new HashSet<string> { "city" });

        columns.Should().Contain(column => column.Name == "city" && column.ExportKey == "audience_city");
        columns.Should().Contain(column => column.Name == "identifier" && column.ExportKey == "identifier");
    }

    [Fact]
    public void Merge_WritesFrozenValuesBesideAnswers()
    {
        IReadOnlyList<AudienceColumn> columns = AudienceExport.Columns([Snapshot], new HashSet<string>());

        using JsonDocument merged = JsonDocument.Parse(AudienceExport.Merge(
            """{"familySize":"3 to 4 people","ageInMonths":"565"}""",
            Snapshot,
            columns));

        merged.RootElement.GetProperty("familySize").GetString().Should().Be("3 to 4 people");
        merged.RootElement.GetProperty("ageInMonths").GetString().Should().Be("565");
        merged.RootElement.GetProperty("identifier").GetString().Should().Be("CG-000020-01");
        merged.RootElement.GetProperty("city").GetString().Should().Be("07. PACIFIC - PORTLAND");
        merged.RootElement.GetProperty("age").GetInt32().Should().Be(47);
        merged.RootElement.GetProperty("family_size").GetString().Should().Be("3 to 4 people");
    }

    [Fact]
    public void Merge_WithoutSnapshot_LeavesAudienceColumnsBlank()
    {
        IReadOnlyList<AudienceColumn> columns = AudienceExport.Columns([Snapshot], new HashSet<string>());

        using JsonDocument merged = JsonDocument.Parse(AudienceExport.Merge(
            """{"familySize":"kept"}""",
            snapshot: null,
            columns));

        merged.RootElement.GetProperty("familySize").GetString().Should().Be("kept");
        merged.RootElement.TryGetProperty("city", out _).Should().BeFalse();
        merged.RootElement.TryGetProperty("identifier", out _).Should().BeFalse();
    }

    [Fact]
    public void Merge_UsesTheSnapshotNotALaterValue()
    {
        const string frozen = """{"identifier":"CG-000020-01","variables":{"city":"07. PACIFIC - PORTLAND"}}""";
        IReadOnlyList<AudienceColumn> columns = AudienceExport.Columns([frozen], new HashSet<string>());

        using JsonDocument merged = JsonDocument.Parse(AudienceExport.Merge("{}", frozen, columns));

        merged.RootElement.GetProperty("city").GetString().Should().Be("07. PACIFIC - PORTLAND");
    }
}
