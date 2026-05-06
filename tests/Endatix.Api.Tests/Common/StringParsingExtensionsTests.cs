using Endatix.Api.Common;

namespace Endatix.Api.Tests.Common;

public class StringParsingExtensionsTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("-1", -1L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void TryParseToLong_WhenValueIsValid_ReturnsTrueAndParsedValue(string input, long expected)
    {
        var success = input.TryParseToLong(out var parsed);

        success.Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2")]
    [InlineData("9223372036854775808")]
    public void TryParseToLong_WhenValueIsInvalid_ReturnsFalseAndDefaultValue(string? input)
    {
        var success = input.TryParseToLong(out var parsed);

        success.Should().BeFalse();
        parsed.Should().Be(default(long));
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("-1", -1L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void ParseToLong_WhenValueIsValid_ReturnsParsedLong(string input, long expected)
    {
        var result = input.ParseToLong();

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2")]
    [InlineData("9223372036854775808")]
    public void ParseToLong_WhenValueIsInvalid_ReturnsNull(string? input)
    {
        var result = input.ParseToLong();

        result.Should().BeNull();
    }
}
