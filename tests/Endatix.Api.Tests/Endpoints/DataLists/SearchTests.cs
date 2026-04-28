using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class SearchTests
{
    private readonly IMediator _mediator;
    private readonly IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> _publicFormAccessPolicy;
    private readonly Search _endpoint;

    public SearchTests()
    {
        _mediator = Substitute.For<IMediator>();
        _publicFormAccessPolicy = Substitute.For<IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>>();
        ICachedData<PublicFormAccessData> cached = new Cached<PublicFormAccessData>(
            PublicFormAccessData.CreatePublicForm(1),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(10),
            "etag-search");
        _publicFormAccessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ICachedData<PublicFormAccessData>>(cached));
        _endpoint = Factory.Create<Search>(_mediator, _publicFormAccessPolicy);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        var request = new SearchDataListItemsRequest
        {
            FormId = 7,
            DataListId = 42,
            Query = "ab",
            Skip = 5,
            Take = 20
        };

        var result = Result.Success(new Paged<IReadOnlyCollection<DataListItemDto>>(
            page: 1,
            pageSize: 20,
            totalRecords: 1,
            totalPages: 1,
            items: [new DataListItemDto(3, "Abc", "1")]));
        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _publicFormAccessPolicy.Received(1).GetAccessData(
            Arg.Is<PublicFormAccessContext>(c =>
                c.FormId == request.FormId &&
                c.Token == request.Token &&
                c.TokenType == request.TokenType),
            Arg.Any<CancellationToken>());

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
            FormId = 1,
            DataListId = 42,
            Query = "ab",
            Skip = 0,
            Take = 10
        };

        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<IReadOnlyCollection<DataListItemDto>>(
                page: 1,
                pageSize: 10,
                totalRecords: 1,
                totalPages: 1,
                items: [new DataListItemDto(3, "Abc", "1")])));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<Paged<IReadOnlyCollection<DataListPublicChoiceModel>>>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(10);
        ok.Value.TotalRecords.Should().Be(1);
        ok.Value.TotalPages.Should().Be(1);
        var item = ok.Value.Items.Single();
        item.Label.Should().Be("Abc");
        item.Value.Should().Be("1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAccessDenied_DoesNotCallMediator()
    {
        _publicFormAccessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<PublicFormAccessData>>.NotFound("Form not found."));

        SearchDataListItemsRequest request = new()
        {
            FormId = 1,
            DataListId = 99,
            Query = "x",
            Skip = 0,
            Take = 10
        };

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<ProblemHttpResult>();
        await _mediator.DidNotReceive().Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>());
    }
}
