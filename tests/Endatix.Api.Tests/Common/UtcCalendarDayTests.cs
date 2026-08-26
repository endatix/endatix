using Endatix.Api.Common;

namespace Endatix.Api.Tests.Common;

public sealed class UtcCalendarDayTests
{
    [Theory]
    [InlineData("2024-01-15", 2024, 1, 15)]
    [InlineData("9999-12-31", 9999, 12, 31)]
    public void InclusiveStartUtc_ParsesCalendarDay(string value, int year, int month, int day)
    {
        var result = UtcCalendarDay.InclusiveStartUtc(value);

        result.Should().Be(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ExclusiveEndUtc_UsesNextDay()
    {
        var result = UtcCalendarDay.ExclusiveEndUtc("2024-01-31");

        result.Should().Be(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ExclusiveEndUtc_ClampsDateOnlyMaxValue()
    {
        var result = UtcCalendarDay.ExclusiveEndUtc("9999-12-31");

        result.Should().Be(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2024-13-01")]
    [InlineData("01-01-2024")]
    public void InclusiveStartUtc_ReturnsNullForInvalid(string? value)
    {
        UtcCalendarDay.InclusiveStartUtc(value).Should().BeNull();
    }

    [Fact]
    public void IsFromOnOrBeforeTo_FalseWhenFromAfterTo()
    {
        UtcCalendarDay.IsFromOnOrBeforeTo("2024-02-01", "2024-01-01").Should().BeFalse();
    }

    [Fact]
    public void IsFromOnOrBeforeTo_TrueWhenEqualOrMissing()
    {
        UtcCalendarDay.IsFromOnOrBeforeTo("2024-01-01", "2024-01-01").Should().BeTrue();
        UtcCalendarDay.IsFromOnOrBeforeTo("2024-01-01", null).Should().BeTrue();
    }
}
