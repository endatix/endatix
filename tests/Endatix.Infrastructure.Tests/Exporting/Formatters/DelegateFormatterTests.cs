using Endatix.Core.Abstractions.Exporting;
using Endatix.Infrastructure.Exporting.Formatters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Exporting.Formatters;

public class DelegateFormatterTests
{
    private readonly TransformationContext<object> _context =
        new(new object(), null, NullLogger.Instance);

    [Fact]
    public void Format_ShouldCallDelegate_WithValue()
    {
        var callCount = 0;
        var receivedValue = (object?)null;
        var formatter = new DelegateFormatter(value =>
        {
            callCount++;
            receivedValue = value;
            return "formatted";
        });

        formatter.Format("test", _context);

        Assert.Equal(1, callCount);
        Assert.Equal("test", receivedValue);
    }

    [Fact]
    public void Format_ShouldReturnDelegateResult()
    {
        var formatter = new DelegateFormatter(_ => "custom-format");

        var result = formatter.Format(42, _context);

        Assert.Equal("custom-format", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hello")]
    [InlineData(123)]
    [InlineData(true)]
    public void Format_ShouldPassValueToDelegate(object? input)
    {
        object? capturedValue = null;
        var formatter = new DelegateFormatter(value =>
        {
            capturedValue = value;
            return value?.ToString() ?? "";
        });

        formatter.Format(input, _context);

        Assert.Equal(input, capturedValue);
    }

    [Fact]
    public void Format_ShouldReturnEmptyString_WhenDelegateReturnsEmpty()
    {
        var formatter = new DelegateFormatter(_ => string.Empty);

        var result = formatter.Format("test", _context);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_ShouldHandleComplexObjects()
    {
        var testObj = new { Name = "Test", Value = 42 };
        var formatter = new DelegateFormatter(v => $"Object: {v}");

        var result = formatter.Format(testObj, _context);

        Assert.Equal("Object: { Name = Test, Value = 42 }", result);
    }
}
