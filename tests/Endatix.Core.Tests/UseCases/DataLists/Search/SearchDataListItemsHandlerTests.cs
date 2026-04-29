using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;

namespace Endatix.Core.Tests.UseCases.DataLists.Search;

public class SearchDataListItemsHandlerTests
{
    private readonly IDataListRepository _repository;
    private readonly SearchDataListItemsHandler _sut;

    public SearchDataListItemsHandlerTests()
    {
        _repository = Substitute.For<IDataListRepository>();
        _sut = new SearchDataListItemsHandler(_repository);
    }

    [Fact]
    public async Task Handle_DataListNotFound_ReturnsNotFound()
    {
        _repository.SearchItemsAsync(
                1,
                null,
                0,
                10,
                Arg.Any<CancellationToken>())
            .Returns((DataListSearchPageResult?)null);

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedItems()
    {
        _repository.SearchItemsAsync(
                1,
                null,
                0,
                10,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(
                1,
                2,
                [
                    new DataListSearchItemResult(1, "New York", "NYC"),
                    new DataListSearchItemResult(2, "Los Angeles", "LA")
                ]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalRecords.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithQuery_FiltersItems()
    {
        _repository.SearchItemsAsync(
                1,
                "New",
                0,
                10,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(
                1,
                1,
                [new DataListSearchItemResult(1, "New York", "NYC")]));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, "New", 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(1);

        await _repository.Received(1).SearchItemsAsync(
            1,
            "New",
            0,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Paging_CorrectlyCalculatesTotal()
    {
        _repository.SearchItemsAsync(
                1,
                null,
                50,
                10,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 50, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(100);
        result.Value.Page.Should().Be(6);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalPages.Should().Be(10);
    }

    [Theory]
    [InlineData(26, 25, 2)]
    [InlineData(51, 25, 3)]
    [InlineData(1, 25, 1)]
    public async Task Handle_NonAlignedOffset_ReturnsCorrectPage(int skip, int take, int expectedPage)
    {
        _repository.SearchItemsAsync(
                1,
                null,
                skip,
                take,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, skip, take),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(expectedPage);
    }

    [Theory]
    [InlineData(100, 10)]
    [InlineData(200, 10)]
    public async Task Handle_OutOfRangeSkip_ReturnsLastPage(int skip, int take)
    {
        _repository.SearchItemsAsync(
                1,
                null,
                skip,
                take,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(1, 100, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, skip, take),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(10);
    }

    [Fact]
    public async Task Handle_SkipBeyondTotalRecords_ReturnsTotalPages()
    {
        _repository.SearchItemsAsync(
                1,
                null,
                0,
                10,
                Arg.Any<CancellationToken>())
            .Returns(new DataListSearchPageResult(1, 0, []));

        var result = await _sut.Handle(
            new SearchDataListItemsQuery(1, null, 0, 10),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(1);
        result.Value.TotalPages.Should().Be(0);
        result.Value.TotalRecords.Should().Be(0);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(-5, 5)]
    public void Handle_WithInvalidQuery_ThrowsArgumentException(int skip, int take)
    {
        var invalidQuery = new SearchDataListItemsQuery(1, null, skip, take);
        Func<Task> act = async () => await _sut.Handle(invalidQuery, TestContext.Current.CancellationToken);

        act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(invalidQuery.Skip));
    }
}