using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Tests.Domain;

public sealed class AudienceImportChunkTests
{
    private static readonly AudienceImport Import = AudienceImport.Start(new AudienceImportStartArgs(
        1, 2, "people.csv", ["email", "city"], "email", new Dictionary<string, string>(), IsTest: false,
        RowCount: 2_500, ChunkSize: 1_000, PerformedById: null));

    [Fact]
    public void Rows_SecondChunk_NumbersEachRowFromItsFirstSpreadsheetRowInOrder()
    {
        // Arrange — chunk 1 of 1,000-row chunks starts after the header row and chunk 0.
        AudienceImportChunk chunk = AudienceImportChunk.Create(Import, new ChunkPlace(1, 1_002),
        [
            new StagedRow(["ada@example.com", "Sofia"], Malformed: false),
            new StagedRow(["bo\"b@example.com", "Varna"], Malformed: true),
            new StagedRow(["cy@example.com"], Malformed: false),
        ]);

        // Act
        IReadOnlyList<NumberedRow> rows = chunk.Rows();

        // Assert
        rows.Select(row => row.RowNumber).Should().Equal(1_002, 1_003, 1_004);
        rows[0].Row.Cells.Should().Equal("ada@example.com", "Sofia");
        rows[1].Row.Malformed.Should().BeTrue();
        rows[2].Row.Cells.Should().Equal("cy@example.com");
        chunk.ChunkIndex.Should().Be(1);
        chunk.RowCount.Should().Be(3);
        chunk.TenantId.Should().Be(Import.TenantId);
    }

    [Fact]
    public void Create_NoRows_IsRefused()
    {
        // Act
        Action act = () => AudienceImportChunk.Create(Import, new ChunkPlace(0, 2), []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 0)]
    public void Create_PlaceOutsideTheFile_IsRefused(int index, int firstRowNumber)
    {
        // Act
        Action act = () => AudienceImportChunk.Create(
            Import, new ChunkPlace(index, firstRowNumber), [new StagedRow(["ada@example.com"], false)]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rows_WithoutTheirOwnNumbers_StoreNoRowNumber()
    {
        // Arrange
        AudienceImportChunk chunk = AudienceImportChunk.Create(
            Import, new ChunkPlace(0, 2), [new StagedRow(["a@example.com"], false), new StagedRow(["b@example.com"], false)]);

        // Act
        IReadOnlyList<NumberedRow> rows = chunk.Rows();

        // Assert — chunks staged before rows carried numbers read the same.
        rows.Select(row => row.RowNumber).Should().Equal(2, 3);
        chunk.RowsJson.Should().NotContain("RowNumber");
    }

    [Fact]
    public void Rows_WithTheirOwnNumbers_KeepThem()
    {
        // Arrange — a blank line was row 3.
        AudienceImportChunk chunk = AudienceImportChunk.Create(
            Import,
            new ChunkPlace(0, 2),
            [new StagedRow(["a@example.com"], false, 2), new StagedRow(["b@example.com"], false, 4)]);

        // Act
        IReadOnlyList<NumberedRow> rows = chunk.Rows();

        // Assert
        rows.Select(row => row.RowNumber).Should().Equal(2, 4);
    }
}
