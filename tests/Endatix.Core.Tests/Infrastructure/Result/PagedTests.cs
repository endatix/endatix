using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.Infrastructure.Result;

public class PagedTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesInstance()
    {
        var items = new[] { "a", "b" };

        var paged = new Paged<string>(1, 10, 15, 2, items);

        paged.Page.Should().Be(1);
        paged.PageSize.Should().Be(10);
        paged.TotalRecords.Should().Be(15);
        paged.TotalPages.Should().Be(2);
        paged.Items.Should().BeEquivalentTo(items);
    }

    [Theory]
    [InlineData(0, 10, 10, 1)]
    [InlineData(-1, 10, 10, 1)]
    public void Constructor_InvalidPage_ThrowsArgumentException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1, 0, 10, 1)]
    [InlineData(1, -5, 10, 1)]
    public void Constructor_InvalidPageSize_ThrowsArgumentException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1, 10, -1, 1)]
    public void Constructor_NegativeTotalRecords_ThrowsArgumentException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1, 10, 10, -1)]
    public void Constructor_NegativeTotalPages_ThrowsArgumentException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullItems_ThrowsArgumentNullException()
    {
        var act = () => new Paged<string>(1, 10, 10, 1, null!);

        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("items");
    }

    [Theory]
    [InlineData(1, 10, 10, 3)]
    [InlineData(1, 10, 15, 1)]
    public void Constructor_InconsistentTotalPages_ThrowsArgumentOutOfRangeException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(3, 10, 20, 2)]
    [InlineData(5, 10, 25, 2)]
    public void Constructor_PageGreaterThanTotalPagesWithItems_ThrowsArgumentOutOfRangeException(int page, int pageSize, long totalRecords, long totalPages)
    {
        var items = new[] { "a", "b" };

        var act = () => new Paged<string>(page, pageSize, totalRecords, totalPages, items);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_PageNotOneWhenTotalPagesZero_ThrowsArgumentOutOfRangeException()
    {
        var items = Array.Empty<string>();

        var act = () => new Paged<string>(2, 10, 0, 0, items);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("page");
    }

    [Fact]
    public void Constructor_EmptyPaged_Succeeds()
    {
        var paged = new Paged<string>(1, 10, 0, 0, []);

        paged.Page.Should().Be(1);
        paged.PageSize.Should().Be(10);
        paged.TotalRecords.Should().Be(0);
        paged.TotalPages.Should().Be(0);
        paged.Items.Should().BeEmpty();
    }

    [Fact]
    public void FromPagedRequest_ValidInput_CreatesInstance()
    {
        var items = new[] { "a", "b" };

        var paged = Paged<string>.FromPagedRequest(0, 10, 15, items);

        paged.Page.Should().Be(1);
        paged.PageSize.Should().Be(10);
        paged.TotalRecords.Should().Be(15);
        paged.TotalPages.Should().Be(2);
        paged.Items.Should().BeEquivalentTo(items);
    }

    [Fact]
    public void FromPagedRequest_SecondPage_CreatesCorrectPage()
    {
        var items = new[] { "c", "d" };

        var paged = Paged<string>.FromPagedRequest(10, 10, 25, items);

        paged.Page.Should().Be(2);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(-5, 10)]
    public void FromPagedRequest_InvalidSkip_ThrowsArgumentException(int skip, int take)
    {
        var items = new[] { "a" };

        var act = () => Paged<string>.FromPagedRequest(skip, take, 10, items);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPagedRequest_TakeIsZero_ThrowsArgumentException()
    {
        var items = new[] { "a" };

        var act = () => Paged<string>.FromPagedRequest(0, 0, 10, items);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromPagedRequest_TotalRecordsZero_ReturnsEmpty()
    {
        var items = Array.Empty<string>();

        var paged = Paged<string>.FromPagedRequest(0, 10, 0, items);

        paged.TotalPages.Should().Be(0);
        paged.Page.Should().Be(1);
    }

    [Fact]
    public void Empty_ValidInput_CreatesEmptyInstance()
    {
        var empty = Paged<string>.Empty(10);

        empty.Page.Should().Be(1);
        empty.PageSize.Should().Be(10);
        empty.TotalRecords.Should().Be(0);
        empty.TotalPages.Should().Be(0);
        empty.Items.Should().BeEmpty();
    }
}