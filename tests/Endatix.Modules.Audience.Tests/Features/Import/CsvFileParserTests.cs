using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Shared;

namespace Endatix.Modules.Audience.Tests.Features.Import;

public sealed class CsvFileParserTests
{
    [Fact]
    public void Parse_WithHeaderAndRows_ReturnsRows()
    {
        // Arrange
        const string csv = """
            email,department
            a@example.com,Eng
            b@example.com,Sales
            """;

        // Act
        CsvFileParseResult result = CsvFileParser.Parse(csv);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("a@example.com", result.Rows[0].Cells["email"]);
        Assert.Equal("Eng", result.Rows[0].Cells["department"]);
    }

    [Fact]
    public void Parse_BlankLineAndMultiLineCell_NumbersRowsAsASpreadsheetShowsThem()
    {
        // Arrange
        const string csv = "email,note\na@example.com,one\n\nb@example.com,\"two\nlines\"\nc@example.com,three\n";

        // Act
        CsvFileParseResult result = CsvFileParser.Parse(csv);

        // Assert
        Assert.Equal([2, 4, 5], result.Rows.Select(row => row.RowNumber));
    }

    [Fact]
    public void Parse_OverMaxDataRows_ReturnsInvalid()
    {
        // Arrange
        string csv = "email\n" + string.Join(
            '\n',
            Enumerable.Range(0, ImportLimits.MaxRows + 1).Select(index => $"p{index}@example.com"));

        // Act
        CsvFileParseResult result = CsvFileParser.Parse(csv);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("5000", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WithEmptyContent_ReturnsInvalid()
    {
        // Act
        CsvFileParseResult result = CsvFileParser.Parse("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("header", result.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
