using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class AnswerFilterExpressionsTests
{
    [Fact]
    public void TryParse_ContainsWithEncodedKey_DecodesKeyAndKeepsColonsInValue()
    {
        // Arrange
        string[] expressions = ["contains:page%3Acity:Co:lumbus "];

        // Act
        var filters = AnswerFilterExpressions.TryParse(expressions);

        // Assert
        filters.Should().Equal(new AnswerFilter("page:city", AnswerFilterOperators.Contains, "Co:lumbus"));
    }

    [Fact]
    public void TryParse_DayBounds_ReturnsFromAndTo()
    {
        // Arrange
        string[] expressions = ["FROM:visit:2026-02-01", "to:visit:2026-02-28"];

        // Act
        var filters = AnswerFilterExpressions.TryParse(expressions);

        // Assert
        filters.Should().Equal(
            new AnswerFilter("visit", AnswerFilterOperators.From, "2026-02-01"),
            new AnswerFilter("visit", AnswerFilterOperators.To, "2026-02-28"));
    }

    [Fact]
    public void TryParse_BlankExpressions_AreSkipped()
    {
        // Act
        var filters = AnswerFilterExpressions.TryParse(["", "  "]);

        // Assert
        filters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("contains:city")]
    [InlineData("contains::text")]
    [InlineData("contains:city:   ")]
    [InlineData("equals:city:x")]
    [InlineData("from:visit:2026-02-30")]
    [InlineData("to:visit:28/02/2026")]
    public void TryParse_MalformedExpression_ReturnsNull(string expression)
    {
        // Act
        var filters = AnswerFilterExpressions.TryParse([expression]);

        // Assert
        filters.Should().BeNull();
    }

    [Fact]
    public void TryParse_TextLongerThanTheLimit_ReturnsNull()
    {
        // Arrange
        var expression = $"contains:city:{new string('x', AnswerFilterExpressions.MaxTextLength + 1)}";

        // Act
        var filters = AnswerFilterExpressions.TryParse([expression]);

        // Assert
        filters.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-02-28", "2026-03-01")]
    [InlineData("2024-12-31", "2025-01-01")]
    public void NextDay_ReturnsTheFollowingCalendarDay(string day, string expected)
    {
        // Act
        var next = AnswerFilterExpressions.NextDay(day);

        // Assert
        next.Should().Be(expected);
    }
}
