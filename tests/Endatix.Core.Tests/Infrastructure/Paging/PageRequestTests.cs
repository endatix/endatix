using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Tests.Infrastructure.Paging;

public sealed class PageRequestTests
{
    [Fact]
    public void Constructor_WhenValuesMissing_UsesDefaults()
    {
        // Act
        var result = new PageRequest(null, null);

        // Assert
        result.Page.Should().Be(PagedRequestLimits.DEFAULT_PAGE);
        result.PageSize.Should().Be(PagedRequestLimits.DEFAULT_PAGE_SIZE);
        result.Skip.Should().Be(0);
    }

    [Fact]
    public void Constructor_WhenPageSizeExceedsMax_ClampsToMaxPageSize()
    {
        // Act
        var result = new PageRequest(1, 500);

        // Assert
        result.PageSize.Should().Be(PagedRequestLimits.MAX_PAGE_SIZE);
    }

    [Fact]
    public void Constructor_WhenPageIsZero_UsesFirstPage()
    {
        // Act
        var result = new PageRequest(0, 10);

        // Assert
        result.Page.Should().Be(PagedRequestLimits.DEFAULT_PAGE);
        result.Skip.Should().Be(0);
    }

    [Fact]
    public void FromSkipTake_WhenSkipProvided_ComputesExpectedPage()
    {
        // Act
        var result = PageRequest.FromSkipTake(skip: 20, take: 10);

        // Assert
        result.Page.Should().Be(3);
        result.PageSize.Should().Be(10);
        result.Skip.Should().Be(20);
    }
}
