using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.Infrastructure.Result;

public class PagedTests
{
    [Fact]
    public void FromSkipAndTake_OutOfRangePage_ReturnsRequestedPageWithEmptyItems()
    {
        // Arrange
        const int skip = 9980;
        const int take = 10;
        const int totalRecords = 25;

        // Act
        var result = Paged<string>.FromSkipAndTake(skip, take, totalRecords, []);

        // Assert
        result.Page.Should().Be(999);
        result.PageSize.Should().Be(take);
        result.TotalRecords.Should().Be(totalRecords);
        result.TotalPages.Should().Be(3);
        result.Items.Should().BeEmpty();
    }
}
