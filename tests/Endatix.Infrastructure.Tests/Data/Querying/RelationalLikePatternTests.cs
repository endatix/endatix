using Endatix.Infrastructure.Data.Querying;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Data.Querying;

public class RelationalLikePatternTests
{
    [Fact]
    public void BuildContainsPattern_SqlServer_EscapesPercentUnderscoreBackslashAndBracket()
    {
        string pattern = RelationalLikePattern.BuildContainsPattern(@"a%b_c\d[e]", sqlServerLike: true);

        pattern.Should().Be(@"%a\%b\_c\\d[[]e]%");
    }

    [Fact]
    public void BuildContainsPattern_Postgres_DoesNotUseSqlServerBracketEscape()
    {
        string pattern = RelationalLikePattern.BuildContainsPattern("a[b]", sqlServerLike: false);

        pattern.Should().Be("%a[b]%");
    }
}
