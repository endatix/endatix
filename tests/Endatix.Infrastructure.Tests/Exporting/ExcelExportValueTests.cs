using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Infrastructure.Exporting;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExcelExportValueTests
{
    [Fact]
    public void Unwrap_JsonBoolean_ReturnsBool()
    {
        // Arrange
        // Act
        var unwrapped = ExcelExportValue.Unwrap(JsonValue.Create(true));

        // Assert
        Assert.Equal(true, unwrapped);
        Assert.Equal(false, ExcelExportValue.Unwrap(JsonValue.Create(false)));
    }

    [Fact]
    public void Unwrap_JsonNumber_ReturnsNumeric()
    {
        // Arrange
        // Act
        var unwrapped = ExcelExportValue.Unwrap(JsonValue.Create(42));

        // Assert
        Assert.Equal(42m, Convert.ToDecimal(unwrapped, CultureInfo.InvariantCulture));
    }

    /// <summary>Both node shapes reach the exporter: JsonElement from the parsed answers, JsonValue from transformers.</summary>
    [Theory]
    [InlineData("2024-06-15T12:00:00Z")]
    [InlineData("2024-06-15T12:00:00")]
    [InlineData("2024-06-15")]
    public void Unwrap_JsonIsoDateString_ReturnsDateTime(string isoDate)
    {
        // Arrange
        using var document = JsonDocument.Parse($"\"{isoDate}\"");

        // Act
        var fromNode = ExcelExportValue.Unwrap(JsonValue.Create(isoDate));
        var fromElement = ExcelExportValue.Unwrap(document.RootElement);

        // Assert
        var expected = new DateTime(2024, 6, 15);
        Assert.Equal(expected.Date, Assert.IsType<DateTime>(fromNode).Date);
        Assert.Equal(expected.Date, Assert.IsType<DateTime>(fromElement).Date);
    }

    [Fact]
    public void Unwrap_PlainString_ReturnsSameString()
    {
        // Arrange
        // Act
        var unwrapped = ExcelExportValue.Unwrap("hello");

        // Assert
        Assert.Equal("hello", unwrapped);
    }

    [Fact]
    public void Unwrap_NonIsoNumericString_StaysString()
    {
        // Arrange
        // Act
        var unwrapped = ExcelExportValue.Unwrap(JsonValue.Create("007"));

        // Assert
        Assert.Equal("007", unwrapped);
    }
}
