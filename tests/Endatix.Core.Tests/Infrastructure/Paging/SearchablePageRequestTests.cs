using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class SearchablePageRequestTests
{
    [Fact]
    public void Constructor_WhenSearchIsWhitespace_NormalizesToNull()
    {
        // Act
        var result = new SearchablePageRequest(1, 10, "   ");

        // Assert
        result.Search.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenSearchHasLeadingAndTrailingSpaces_TrimsValue()
    {
        // Act
        var result = new SearchablePageRequest(1, 10, "  admin@test.com  ");

        // Assert
        result.Search.Should().Be("admin@test.com");
    }

    [Fact]
    public void Constructor_WhenSearchExceedsMaxLength_TruncatesValue()
    {
        // Arrange
        var search = new string('a', PagedRequestLimits.MAX_SEARCH_LENGTH + 10);

        // Act
        var result = new SearchablePageRequest(1, 10, search);

        // Assert
        result.Search.Should().HaveLength(PagedRequestLimits.MAX_SEARCH_LENGTH);
    }
}
