using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Endpoints.Public.DataLists;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class SearchTests
{
    private const string TEST_FORM_ACCESS_JWT = "test-form-access-jwt";

    private static readonly long[] _formLists = [11, 22, 42, 99];

    private readonly IMediator _mediator;
    private readonly IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> _publicFormAccessPolicy;
    private readonly IFormRuntimeScopeReader _runtimeScopeReader;
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
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {TEST_FORM_ACCESS_JWT}";
        _runtimeScopeReader = Substitute.For<IFormRuntimeScopeReader>();
        _runtimeScopeReader
            .ReachesDataListAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<DataListReach>(), Arg.Any<CancellationToken>())
            .Returns(call => _formLists.Contains(call.ArgAt<long>(1)));
        _endpoint = Factory.Create<Search>(httpContext, _mediator, _publicFormAccessPolicy, _runtimeScopeReader);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        var request = new SearchDataListItemsRequest
        {
            FormId = 7,
            DataListId = 42,
            Query = "ab",
            MatchMode = DataListSearchMatchMode.StartsWith,
            Locale = "es",
            IncludeLocales = ["FR,de"],
            Skip = 5,
            Take = 20
        };

        var result = Result.Success(new Paged<DataListItemDto>(
            page: 1,
            pageSize: 20,
            totalRecords: 1,
            totalPages: 1,
            items:
            [
                new DataListItemDto(
                    3,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["default"] = "Abc" },
                    "1",
                    "Abc")
            ]));
        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _publicFormAccessPolicy.Received(1).GetAccessData(
            Arg.Is<PublicFormAccessContext>(c =>
                c.FormId == request.FormId &&
                c.Token == TEST_FORM_ACCESS_JWT &&
                c.TokenType == SubmissionTokenType.FormToken),
            Arg.Any<CancellationToken>());

        await _mediator.Received(1).Send(
            Arg.Is<SearchDataListItemsQuery>(x =>
                x.DataListId == request.DataListId &&
                x.Query == request.Query &&
                x.MatchMode == DataListSearchMatchMode.StartsWith &&
                x.Locale == CultureCode.Parse("es") &&
                x.IncludeLocales.Count == 2 &&
                x.IncludeLocales[0] == CultureCode.Parse("fr") &&
                x.IncludeLocales[1] == CultureCode.Parse("de") &&
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
            .Returns(Result.Success(new Paged<DataListItemDto>(
                page: 1,
                pageSize: 10,
                totalRecords: 1,
                totalPages: 1,
                items:
                [
                    new DataListItemDto(
                        3,
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "Abc",
                            ["es"] = "Abecé"
                        },
                        "1",
                        "Abc")
                ])));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<Paged<DataListPublicChoiceModel>>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(10);
        ok.Value.TotalRecords.Should().Be(1);
        ok.Value.TotalPages.Should().Be(1);
        var item = ok.Value.Items.Single();
        item.Value.Should().Be("1");
        item.Labels.Should().BeEquivalentTo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["default"] = "Abc",
            ["es"] = "Abecé"
        });
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

    [Fact]
    public async Task ExecuteAsync_WhenTheFormDoesNotUseTheList_ReturnsNotFoundWithoutQuerying()
    {
        var request = new SearchDataListItemsRequest { FormId = 1, DataListId = 555, Query = "x", Skip = 0, Take = 10 };

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        var problem = response.Result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Detail.Should().Be("Data list not found.");
        await _mediator.DidNotReceive().Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ChecksTheListAgainstActiveDefinition()
    {
        var request = new SearchDataListItemsRequest { FormId = 1, DataListId = 11, Query = "x", Skip = 0, Take = 10 };
        _mediator.Send(Arg.Any<SearchDataListItemsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new Paged<DataListItemDto>(page: 1, pageSize: 10, totalRecords: 0, totalPages: 0, items: [])));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _runtimeScopeReader.Received(1).ReachesDataListAsync(1, 11, DataListReach.ActiveDefinition, Arg.Any<CancellationToken>());
    }
}
