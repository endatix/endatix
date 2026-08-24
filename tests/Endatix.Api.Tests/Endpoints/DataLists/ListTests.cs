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
                new DataListDto(11, "Cities", null, DateTime.UtcNow, null, true, 0, "en", [], [])
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
            .Returns(Result.Success(new Paged<DataListDto>(1, 25, 0, 0, Array.Empty<DataListDto>())));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ListDataListsQuery>(x => x.Page == 2 && x.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesSearchAndHasLocaleToQuery()
    {
        DataListsListRequest request = new() { Search = "cities", HasLocale = "es" };
        _mediator.Send(Arg.Any<ListDataListsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListDto>(1, 10, 0, 0, Array.Empty<DataListDto>())));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ListDataListsQuery>(x => x.Search == "cities" && x.HasLocale == "es"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesSortAndDateBoundsToQuery()
    {
        DataListsListRequest request = new()
        {
            SortBy = "name",
            SortDir = "asc",
            CreatedFrom = "2024-01-01",
            CreatedTo = "2024-01-31",
            ModifiedFrom = "2024-02-01",
            ModifiedTo = "2024-02-28"
        };
        _mediator.Send(Arg.Any<ListDataListsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListDto>(1, 10, 0, 0, Array.Empty<DataListDto>())));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ListDataListsQuery>(x =>
                x.SortBy == DataListListSortBy.Name &&
                x.SortDescending == false &&
                x.CreatedFrom == new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
                x.CreatedTo == new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) &&
                x.ModifiedFrom == new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) &&
                x.ModifiedTo == new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DateBoundAtCalendarMaximum_DoesNotThrow()
    {
        // Regression guard: ParseExclusiveDayEndUtc must clamp instead of
        // overflowing when CreatedTo/ModifiedTo is the last representable
        // calendar date (DateOnly.MaxValue has no "next day").
        DataListsListRequest request = new()
        {
            CreatedTo = "9999-12-31",
            ModifiedTo = "9999-12-31"
        };
        _mediator.Send(Arg.Any<ListDataListsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListDto>(1, 10, 0, 0, Array.Empty<DataListDto>())));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<Ok<Paged<DataListModel>>>();
        await _mediator.Received(1).Send(
            Arg.Is<ListDataListsQuery>(x =>
                x.CreatedTo == DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc) &&
                x.ModifiedTo == DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
    }
}
