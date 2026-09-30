using Endatix.Modules.Audience.Features.Import;

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
        Assert.Equal("a@example.com", result.Rows[0]["email"]);
        Assert.Equal("Eng", result.Rows[0]["department"]);
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
