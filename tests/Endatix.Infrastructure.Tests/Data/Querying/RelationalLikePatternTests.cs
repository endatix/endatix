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

    [Fact]
    public void BuildPattern_StartsWith_AppendsPercentOnlyAtEnd()
    {
        string pattern = RelationalLikePattern.BuildPattern("App", RelationalTextMatchMode.StartsWith, sqlServerLike: false);

        pattern.Should().Be("App%");
    }

    [Fact]
    public void BuildPattern_Exact_HasNoWildcards()
    {
        string pattern = RelationalLikePattern.BuildPattern("Apple", RelationalTextMatchMode.Exact, sqlServerLike: false);

        pattern.Should().Be("Apple");
    }

    [Fact]
    public void BuildPattern_Exact_StillEscapesMetacharacters()
    {
        string pattern = RelationalLikePattern.BuildPattern("100%", RelationalTextMatchMode.Exact, sqlServerLike: true);

        pattern.Should().Be(@"100\%");
    }
}
