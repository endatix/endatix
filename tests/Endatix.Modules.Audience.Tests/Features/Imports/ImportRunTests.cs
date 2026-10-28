using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.Imports;

namespace Endatix.Modules.Audience.Tests.Features.Imports;

public sealed class ImportRunTests
{
    [Fact]
    public void ToCsvRow_StagedRow_PairsCellsWithTheHeadersByPosition()
    {
        // Arrange
        NumberedRow row = new(5, new StagedRow(["ada@example.com", "Sofia"], Malformed: true));

        // Act
        CsvDataRow csv = ImportRun.ToCsvRow(row, ["Email", "City"]);

        // Assert
        csv.RowNumber.Should().Be(5);
        csv.Malformed.Should().BeTrue();
        csv.Cells["email"].Should().Be("ada@example.com");
        csv.Cells["CITY"].Should().Be("Sofia");
    }

    [Fact]
    public void ToCsvRow_RowLongerOrShorterThanTheHeaders_ReadsOnlyTheCellsBothHave()
    {
        // Arrange
        NumberedRow longRow = new(2, new StagedRow(["ada@example.com", "Sofia", "extra"], Malformed: false));
        NumberedRow shortRow = new(3, new StagedRow(["bob@example.com"], Malformed: false));

        // Act
        CsvDataRow fromLong = ImportRun.ToCsvRow(longRow, ["Email", "City"]);
        CsvDataRow fromShort = ImportRun.ToCsvRow(shortRow, ["Email", "City"]);

        // Assert
        fromLong.Cells.Keys.Should().BeEquivalentTo("Email", "City");
        fromShort.Cells.Keys.Should().BeEquivalentTo("Email");
    }

    [Fact]
    public void ToCsvRow_RepeatedHeader_KeepsTheFirstColumn()
    {
        // Arrange
        NumberedRow row = new(2, new StagedRow(["ada@example.com", "other@example.com"], Malformed: false));

        // Act
        CsvDataRow csv = ImportRun.ToCsvRow(row, ["Email", "email"]);

        // Assert
        csv.Cells.Should().ContainSingle().Which.Value.Should().Be("ada@example.com");
    }
}
