using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ListItemsTests
{
    private readonly IMediator _mediator;
    private readonly ListItems _endpoint;

    public ListItemsTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListItems>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_MapsPagingAndDoesNotRequireActiveList()
    {
        ListDataListItemsRequest request = new()
        {
            DataListId = 42,
            Query = "york",
            Page = 2,
            PageSize = 10,
            MatchMode = DataListSearchMatchMode.StartsWith,
            Locale = "es",
            IncludeLocales = ["fr"]
        };

        var result = Result.Success(new Paged<DataListItemDto>(
            page: 2,
            pageSize: 10,
            totalRecords: 11,
            totalPages: 2,
            items:
            [
                new DataListItemDto(
                    3,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["default"] = "New York" },
                    "NYC",
                    "New York")
            ]));
        _mediator.Send(Arg.Any<SearchManagedDataListItems>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var ok = response.Result.Should().BeOfType<Ok<Paged<DataListItemModel>>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Items.Should().ContainSingle(x => x.Value == "NYC" && x.Id == 3);

        await _mediator.Received(1).Send(
            Arg.Is<SearchManagedDataListItems>(x =>
                x.Search.DataListId == 42 &&
                x.Search.Query == "york" &&
                x.Search.Skip == 10 &&
                x.Search.Take == 10 &&
                x.Search.MatchMode == DataListSearchMatchMode.StartsWith &&
                x.Search.Locale != null &&
                x.Search.IncludeLocales.Count == 1 &&
                x.Search.RequireActive == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NullPaging_UsesDefaultPageSizeAndZeroSkip()
    {
        ListDataListItemsRequest request = new() { DataListId = 5 };
        _mediator.Send(Arg.Any<SearchManagedDataListItems>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListItemDto>(1, ListItems.DefaultPageSize, 0, 0, [])));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<SearchManagedDataListItems>(x =>
                x.Search.DataListId == 5 &&
                x.Search.Skip == 0 &&
                x.Search.Take == ListItems.DefaultPageSize &&
                x.Search.RequireActive == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NotFound_ReturnsProblem()
    {
        _mediator.Send(Arg.Any<SearchManagedDataListItems>(), Arg.Any<CancellationToken>())
            .Returns(Result<Paged<DataListItemDto>>.NotFound("Data list not found."));

        var response = await _endpoint.ExecuteAsync(
            new ListDataListItemsRequest { DataListId = 9 },
            TestContext.Current.CancellationToken);

        response.Result.Should().NotBeOfType<Ok<Paged<DataListItemModel>>>();
    }
}
