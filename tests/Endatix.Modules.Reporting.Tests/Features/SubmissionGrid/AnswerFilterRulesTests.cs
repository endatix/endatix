using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class AnswerFilterRulesTests
{
    private static readonly FormSearchKeys SearchKeys = FormSearchKeys.FromJson("""
        [
          {"key":"city","source":"auto","filterType":"text","addedAt":"2026-10-16T00:00:00Z"},
          {"key":"visit","source":"auto","filterType":"date","addedAt":"2026-10-16T00:00:00Z"},
          {"key":"arrival","source":"user","filterType":"datetime","addedAt":"2026-10-16T00:00:00Z"}
        ]
        """);

    [Fact]
    public void Check_SupportedOperatorsOnSearchKeys_HasNoErrors()
    {
        // Arrange
        AnswerFilter[] filters =
        [
            new("city", AnswerFilterOperators.Contains, "col"),
            new("visit", AnswerFilterOperators.From, "2026-01-01"),
            new("arrival", AnswerFilterOperators.To, "2026-01-31"),
        ];

        // Act
        var errors = AnswerFilterRules.Check(filters, SearchKeys);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Check_KeyThatIsNotASearchKey_ReportsIt()
    {
        // Act
        var errors = AnswerFilterRules.Check([new("notes", AnswerFilterOperators.Contains, "x")], SearchKeys);

        // Assert
        var error = errors.Should().ContainSingle().Subject;
        error.Identifier.Should().Be(AnswerFilterRules.Field);
        error.ErrorCode.Should().Be(AnswerFilterRules.UnknownKeyCode);
    }

    [Theory]
    [InlineData("city", AnswerFilterOperators.From)]
    [InlineData("visit", AnswerFilterOperators.Contains)]
    public void Check_OperatorTheColumnDoesNotSupport_ReportsIt(string key, string @operator)
    {
        // Act
        var errors = AnswerFilterRules.Check([new(key, @operator, "2026-01-01")], SearchKeys);

        // Assert
        errors.Should().ContainSingle(error => error.ErrorCode == AnswerFilterRules.UnsupportedOperatorCode);
    }
}
