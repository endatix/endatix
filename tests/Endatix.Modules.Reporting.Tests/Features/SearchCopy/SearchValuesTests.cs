using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class SearchValuesTests
{
    [Theory]
    [InlineData("Acme Corp", "acme corp")]
    [InlineData("ФИРМА", "фирма")]
    [InlineData("2026-03-01T10:00", "2026-03-01t10:00")]
    public void Normalize_Text_Lowercases(string value, string expected)
    {
        // Act
        var normalized = SearchValues.Normalize(value, SubmissionGridFilterTypes.Text);

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-03-01", "2026-03-01")]
    [InlineData("2026-03-01T10:30:00Z", "2026-03-01")]
    [InlineData("1 March 2026", "1 March 2026")]
    [InlineData("", "")]
    public void Normalize_Date_KeepsTheCalendarDayOrTheValueAsEntered(string value, string expected)
    {
        // Act
        var normalized = SearchValues.Normalize(value, SubmissionGridFilterTypes.Date);

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-03-01T10:30", "2026-03-01T10:30:00")]
    [InlineData("2026-03-01 10:30:45", "2026-03-01T10:30:45")]
    [InlineData("2026-03-01t10:30:45.123+02:00", "2026-03-01T10:30:45")]
    [InlineData("2026-03-01", "2026-03-01T00:00:00")]
    [InlineData("2026-03-01\n", "2026-03-01\n")]
    [InlineData("tomorrow", "tomorrow")]
    public void Normalize_DateTime_WritesSecondsOrTheValueAsEntered(string value, string expected)
    {
        // Act
        var normalized = SearchValues.Normalize(value, SubmissionGridFilterTypes.DateTime);

        // Assert
        normalized.Should().Be(expected);
    }
}
