using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class SearchTests
{
    private readonly IMediator _mediator;
    private readonly Search _endpoint;

    public SearchTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Search>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        var request = new SearchDataListItemsRequest
        {
            DataListId = 42,
            Query = "ab",
            Skip = 5,
            Take = 20
        };

        var result = Result.Success(new SearchDataListItemsDto(
            42,
            1,
            5,
            20,
            [new DataListItemDto(3, "Abc", "1")]));
        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<SearchDataListItemsQuery>(x =>
                x.DataListId == request.DataListId &&
                x.Query == request.Query &&
                x.Skip == request.Skip &&
                x.Take == request.Take),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPublicItems()
    {
        SearchDataListItemsRequest request = new()
        {
            DataListId = 42,
            Query = "ab",
            Skip = 0,
            Take = 10
        };

        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new SearchDataListItemsDto(
                42,
                1,
                0,
                10,
                [new DataListItemDto(3, "Abc", "1")])));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<DataListPublicSearchResultModel>>().Subject;
        var item = ok.Value.Items.Single();
        item.Label.Should().Be("Abc");
        item.Value.Should().Be("1");
    }
}
