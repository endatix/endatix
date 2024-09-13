using Endatix.Api.Infrastructure.Cors;

namespace Endatix.Api.Tests.Infrastructure.Cors;

public class CorsWildcardSearcherTests
{

    private readonly CorsWildcardSearcher _searcher;
    public CorsWildcardSearcherTests()
    {
        _searcher = new CorsWildcardSearcher();
    }

    [Fact]
    public void SearchForWildcard_EmptyOrNullInput_ReturnsNone()
    {
        // Arrange
        IList<string> emptyArray = [];
        IList<string> nullArray = null;

        // Act
        var emptyArrayResult = _searcher.SearchForWildcard(emptyArray);
        var nullArrayResult = _searcher.SearchForWildcard(nullArray);

        // Assert
        emptyArrayResult.Should().Be(CorsWildcardResult.None);
        nullArrayResult.Should().Be(CorsWildcardResult.None);
    }

    public static IEnumerable<object[]> GetValidAsteriskInputs()
    {
        yield return new object[] { new string[] { "*" } };
        yield return new object[] { new string[] { "https://some.origin", "asterisk.with.whitespace.should.be.correct", " *", "everything.after.it.should.be.ignored", "-" } };
        yield return new object[] { new string[] { "https://some.origin", " *" } };
    }

    [Theory]
    [MemberData(nameof(GetValidAsteriskInputs))]
    public void SearchForWildcard_ValidAsteriskInput_ReturnsMatchAll(IList<string> searchInput)
    {
        // Arrange

        // Act
        var searcherResult = _searcher.SearchForWildcard(searchInput);

        // Assert
        searcherResult.Should().Be(CorsWildcardResult.MatchAll);
    }

    public static IEnumerable<object[]> GetIgnoreWildcardInputs()
    {
        yield return new object[] { new string[] { "-" } };
        yield return new object[] { new string[] { "https://some.origin", " -", "everything.after.it.should.be.ignored", "*" } };
        yield return new object[] { new string[] { "https://some.origin", " -    ", "*" } };
    }

    [Theory]
    [MemberData(nameof(GetIgnoreWildcardInputs))]
    public void SearchForWildcard_IgnoreInput_ReturnsIgnoreAll(string[] searchInput)
    {
        // Arrange

        // Act
        var searcherResult = _searcher.SearchForWildcard(searchInput);

        // Assert
        searcherResult.Should().Be(CorsWildcardResult.IgnoreAll);
    }

     public static IEnumerable<object[]> GetValidOriginInputs()
    {
        yield return new object[] { new string[] { "" } };
        yield return new object[] { new string[] { "https://some.origin", "anotherInput", "everything.after.it.should.be.ignored" } };
        yield return new object[] { new string[] { "    ", "ftps:4220", "https://gist.github.com/", "https://localhost:5220"} };
    }

    [Theory]
    [MemberData(nameof(GetValidOriginInputs))]
    public void SearchForWildcard_NoWildcard_ReturnsNone(string[] searchInput)
    {
        // Arrange

        // Act
        var searcherResult = _searcher.SearchForWildcard(searchInput);

        // Assert
        searcherResult.Should().Be(CorsWildcardResult.None);
    }
}
