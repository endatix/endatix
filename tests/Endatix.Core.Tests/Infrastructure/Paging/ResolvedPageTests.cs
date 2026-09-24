using Endatix.Core.Infrastructure.Paging;
using FluentAssertions;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class ResolvedPageTests
{
    [Fact]
    public void For_PagePastTheEnd_ClampsAndSkipsTheLastPage()
    {
        // Arrange
        var request = new PageRequest(page: 5, pageSize: 2);

        // Act
        var window = request.ForTotal(totalRecords: 3);

        // Assert
        window.Page.Should().Be(2);
        window.Skip.Should().Be(2);
    }

    [Fact]
    public void SafeSkip_HugePage_DoesNotGoNegative()
    {
        // Act
        var skip = ResolvedPage.SafeSkip(page: int.MaxValue, pageSize: 100);

        // Assert
        skip.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ToPaged_FetchLargerThanCount_RaisesTotal()
    {
        // Arrange
        var window = new PageRequest(1, 10).ForTotal(0);

        // Act
        var paged = window.ToPaged(totalRecords: 0, items: ["new"]);

        // Assert
        paged.TotalRecords.Should().Be(1);
        paged.Items.Should().ContainSingle();
    }
}
