using System.Globalization;
using System.Text.Json.Nodes;
using Endatix.Infrastructure.Exporting;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class ExcelExportValueTests
{
    [Fact]
    public void Unwrap_JsonBoolean_ReturnsBool()
    {
        Assert.Equal(true, ExcelExportValue.Unwrap(JsonValue.Create(true)));
        Assert.Equal(false, ExcelExportValue.Unwrap(JsonValue.Create(false)));
    }

    [Fact]
    public void Unwrap_JsonNumber_ReturnsNumeric()
    {
        Assert.Equal(42m, Convert.ToDecimal(ExcelExportValue.Unwrap(JsonValue.Create(42)), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Unwrap_JsonIsoDateString_ReturnsDateTime()
    {
        var unwrapped = ExcelExportValue.Unwrap(JsonValue.Create("2024-06-15T12:00:00Z"));
        var dateTime = Assert.IsType<DateTime>(unwrapped);
        Assert.Equal(2024, dateTime.Year);
        Assert.Equal(6, dateTime.Month);
        Assert.Equal(15, dateTime.Day);
    }

    [Fact]
    public void Unwrap_PlainString_Unchanged()
    {
        Assert.Equal("hello", ExcelExportValue.Unwrap("hello"));
    }
}
