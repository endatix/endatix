using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class PageWindowTests
{
    [Theory]
    [InlineData("empty list", 1, 10, 0, 1, 0)]
    [InlineData("page past the end of an empty list", 3, 10, 0, 1, 0)]
    [InlineData("first page", 1, 10, 25, 1, 0)]
    [InlineData("partial last page", 3, 10, 25, 3, 20)]
    [InlineData("page past the end", 4, 10, 25, 3, 20)]
    [InlineData("exact multiple, last page", 2, 10, 20, 2, 10)]
    [InlineData("exact multiple, one past the end", 3, 10, 20, 2, 10)]
    [InlineData("int.MaxValue page", int.MaxValue, 100, 250, 3, 200)]
    [InlineData("zero page", 0, 10, 5, 1, 0)]
    [InlineData("negative page", -5, 10, 5, 1, 0)]
    [InlineData("int.MaxValue rows", int.MaxValue, 100, int.MaxValue, 21474837, 2147483600)]
    public void For_RequestedPage_ClampsToExistingRows(
        string scenario,
        int page,
        int pageSize,
        int totalRecords,
        int expectedPage,
        int expectedSkip)
    {
        // Act
        var window = PageWindow.For(page, pageSize, totalRecords);

        // Assert
        window.Page.Should().Be(expectedPage, scenario);
        window.Skip.Should().Be(expectedSkip, scenario);
        window.PageSize.Should().Be(pageSize, scenario);
        window.TotalRecords.Should().Be(totalRecords, scenario);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public void For_InvalidPageSizeOrTotal_Throws(int pageSize, int totalRecords)
    {
        // Act
        var act = () => PageWindow.For(1, pageSize, totalRecords);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToPaged_FullPage_ReportsPageAndTotals()
    {
        // Arrange
        var window = PageWindow.For(page: 2, pageSize: 10, totalRecords: 25);
        var items = Enumerable.Range(11, 10).ToList();

        // Act
        var paged = window.ToPaged(items);

        // Assert
        paged.Page.Should().Be(2);
        paged.PageSize.Should().Be(10);
        paged.TotalRecords.Should().Be(25);
        paged.TotalPages.Should().Be(3);
        paged.Items.Should().Equal(items);
    }

    [Fact]
    public void ToPaged_EmptyList_ReturnsEmptyFirstPage()
    {
        // Arrange
        var window = PageWindow.For(page: 4, pageSize: 10, totalRecords: 0);

        // Act
        var paged = window.ToPaged<int>([]);

        // Assert
        paged.Page.Should().Be(1);
        paged.TotalRecords.Should().Be(0);
        paged.TotalPages.Should().Be(0);
        paged.Items.Should().BeEmpty();
    }

    [Fact]
    public void ToPaged_RowsAddedAfterCount_RaisesTotalInsteadOfThrowing()
    {
        // Arrange
        var window = PageWindow.For(page: 3, pageSize: 10, totalRecords: 25);

        // Act
        var paged = window.ToPaged(Enumerable.Range(21, 8).ToList());

        // Assert
        paged.Page.Should().Be(3);
        paged.TotalRecords.Should().Be(28);
        paged.Items.Should().HaveCount(8);
    }

    [Fact]
    public void ToPaged_RowAddedToListCountedEmpty_RaisesTotal()
    {
        // Arrange
        var window = PageWindow.For(page: 1, pageSize: 10, totalRecords: 0);

        // Act
        var paged = window.ToPaged(["new"]);

        // Assert
        paged.TotalRecords.Should().Be(1);
        paged.Items.Should().ContainSingle();
    }

    [Fact]
    public void ToPaged_RowsDeletedAfterCount_KeepsTotalAndReturnsShortPage()
    {
        // Arrange
        var window = PageWindow.For(page: 3, pageSize: 10, totalRecords: 25);

        // Act
        var paged = window.ToPaged<int>([]);

        // Assert
        paged.Page.Should().Be(3);
        paged.TotalRecords.Should().Be(25);
        paged.Items.Should().BeEmpty();
    }

    [Fact]
    public void PageRequestSkip_HugePage_SaturatesInsteadOfOverflowing()
    {
        // Arrange
        var request = new PageRequest(page: int.MaxValue, pageSize: 100);

        // Act
        var skip = request.Skip;

        // Assert
        skip.Should().Be(int.MaxValue);
    }

    [Fact]
    public void PageRequestForTotal_PagePastTheEnd_ReturnsLastPageWindow()
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
    public void PagingParametersForTotal_KeepsPageSizeAboveTheRequestLimit()
    {
        // Arrange
        var paging = new PagingParameters(page: 9, pageSize: 500);

        // Act
        var window = paging.ForTotal(totalRecords: 1200);
        var fetch = new PagingParameters(window);

        // Assert
        window.PageSize.Should().Be(500);
        fetch.Page.Should().Be(3);
        fetch.PageSize.Should().Be(500);
    }
}
