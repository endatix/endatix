using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public sealed class AudienceImportChunkTests
{
    private static readonly AudienceImport Import = AudienceImport.Start(new AudienceImportStartArgs(
        1, 2, "people.csv", ["email"], "email", new Dictionary<string, string>(), false, 3, 1_000, null));

    [Fact]
    public void Rows_WithoutTheirOwnNumbers_FollowTheChunksFirstRow()
    {
        // Arrange
        AudienceImportChunk chunk = AudienceImportChunk.Create(
            Import, new ChunkPlace(0, 2), [new(["a@example.com"], false), new(["b@example.com"], false)]);

        // Act
        IReadOnlyList<NumberedRow> rows = chunk.Rows();

        // Assert
        Assert.Equal([2, 3], rows.Select(row => row.RowNumber));
        Assert.DoesNotContain("RowNumber", chunk.RowsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_WithTheirOwnNumbers_KeepThem()
    {
        // Arrange — a blank line was row 3.
        AudienceImportChunk chunk = AudienceImportChunk.Create(
            Import, new ChunkPlace(0, 2), [new(["a@example.com"], false, 2), new(["b@example.com"], false, 4)]);

        // Act
        IReadOnlyList<NumberedRow> rows = chunk.Rows();

        // Assert
        Assert.Equal([2, 4], rows.Select(row => row.RowNumber));
    }
}
