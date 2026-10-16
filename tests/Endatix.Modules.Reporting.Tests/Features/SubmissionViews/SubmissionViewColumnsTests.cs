using System.Text.Json;
using Endatix.Modules.Reporting.Features.SubmissionViews;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionViews;

public sealed class SubmissionViewColumnsTests
{
    private static readonly IReadOnlySet<string> _schemaKeys = new HashSet<string> { "q1", "q2" };

    [Fact]
    public void KeepKnown_EveryKeyKnown_ReturnsSettingsUnchanged()
    {
        // Arrange
        const string settings = """{"v":1,"columns":[{"key":"q2","width":90},{"key":"createdAt"}],"pageSize":25}""";

        // Act
        var kept = SubmissionViewColumns.KeepKnown(settings, _schemaKeys);

        // Assert
        kept.Should().BeSameAs(settings);
    }

    [Fact]
    public void KeepKnown_KeyMissingFromSchema_DropsOnlyThatColumn()
    {
        // Arrange
        const string settings =
            """{"v":1,"columns":[{"key":"q1","visible":false},{"key":"removed","width":80},{"key":"status"}],"pageSize":25,"sort":{"key":"createdAt","direction":"asc"},"density":"compact"}""";

        // Act
        var kept = SubmissionViewColumns.KeepKnown(settings, _schemaKeys);

        // Assert
        using var document = JsonDocument.Parse(kept);
        var root = document.RootElement;
        root.GetProperty("columns").EnumerateArray()
            .Select(column => column.GetProperty("key").GetString())
            .Should().Equal("q1", "status");
        root.GetProperty("columns")[0].GetProperty("visible").GetBoolean().Should().BeFalse();
        root.GetProperty("pageSize").GetInt32().Should().Be(25);
        root.GetProperty("sort").GetProperty("key").GetString().Should().Be("createdAt");
        root.GetProperty("density").GetString().Should().Be("compact");
    }

    [Fact]
    public void KeepKnown_NoSchemaKeys_KeepsOnlyMetadataColumns()
    {
        // Arrange
        const string settings = """{"v":1,"columns":[{"key":"q1"},{"key":"completedAt"}]}""";
        IReadOnlySet<string> noSchema = new HashSet<string>();

        // Act
        var kept = SubmissionViewColumns.KeepKnown(settings, noSchema);

        // Assert
        kept.Should().Be("""{"v":1,"columns":[{"key":"completedAt"}]}""");
    }
}
