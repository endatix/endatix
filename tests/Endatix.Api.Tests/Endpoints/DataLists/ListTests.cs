using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ListTests
{
    private readonly IMediator _mediator;
    private readonly List _endpoint;

    public ListTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<List>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOk()
    {
        var payload = new Paged<DataListDto>(
            page: 1,
            pageSize: 10,
            totalRecords: 1,
            totalPages: 1,
            items:
            [
                new DataListDto(11, "Cities", null, true, [])
            ]);
        var result = Result.Success(payload);
        _mediator.Send(Arg.Any<ListDataListsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _endpoint.ExecuteAsync(new DataListsListRequest(), TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<Paged<DataListModel>>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(10);
        ok.Value.TotalRecords.Should().Be(1);
        ok.Value.TotalPages.Should().Be(1);
        ok.Value.Items.Should().ContainSingle(x => x.Id == 11 && x.Name == "Cities");
    }

    [Fact]
    public async Task ExecuteAsync_PassesPagingToQuery()
    {
        DataListsListRequest request = new() { Page = 2, PageSize = 25 };
        _mediator.Send(Arg.Any<ListDataListsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListDto>(2, 25, 0, 0, [])));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ListDataListsQuery>(x => x.Page == 2 && x.PageSize == 25),
            Arg.Any<CancellationToken>());
    }
}
