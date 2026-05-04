using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Endpoints.Public.DataLists;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class GetChoiceDisplayValuesTests
{
    private readonly IMediator _mediator;
    private readonly IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> _publicFormAccessPolicy;
    private readonly GetChoiceDisplayValues _endpoint;

    public GetChoiceDisplayValuesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _publicFormAccessPolicy = Substitute.For<IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext>>();
        ICachedData<PublicFormAccessData> cached = new Cached<PublicFormAccessData>(
            PublicFormAccessData.CreatePublicForm(1),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(10),
            "etag-display");
        _publicFormAccessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ICachedData<PublicFormAccessData>>(cached));
        _endpoint = Factory.Create<GetChoiceDisplayValues>(_mediator, _publicFormAccessPolicy);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        GetChoiceDisplayValuesRequest request = new() { FormId = 3, DataListId = 11, Values = ["a", "b"] };
        _mediator.Send(Arg.Any<GetDataListChoiceDisplayValuesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DataListChoiceDisplayValueDto>>([]));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _publicFormAccessPolicy.Received(1).GetAccessData(
            Arg.Is<PublicFormAccessContext>(c =>
                c.FormId == request.FormId &&
                c.Token == request.Token &&
                c.TokenType == request.TokenType),
            Arg.Any<CancellationToken>());

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListChoiceDisplayValuesQuery>(x =>
                x.DataListId == request.DataListId &&
                x.Values.SequenceEqual(request.Values)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPublicChoiceModels()
    {
        GetChoiceDisplayValuesRequest request = new() { FormId = 2, DataListId = 11, Values = ["1"] };
        _mediator.Send(Arg.Any<GetDataListChoiceDisplayValuesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DataListChoiceDisplayValueDto>>(
                [new DataListChoiceDisplayValueDto("1", "United States")]));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>>().Subject;
        ok.Value.Should().ContainSingle(x => x.Value == "1" && x.Label == "United States");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAccessDenied_DoesNotCallMediator()
    {
        _publicFormAccessPolicy
            .GetAccessData(Arg.Any<PublicFormAccessContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<ICachedData<PublicFormAccessData>>.NotFound("Form not found."));

        GetChoiceDisplayValuesRequest request = new() { FormId = 1, DataListId = 22, Values = ["1"] };

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        response.Result.Should().BeOfType<ProblemHttpResult>();
        await _mediator.DidNotReceive().Send(Arg.Any<GetDataListChoiceDisplayValuesQuery>(), Arg.Any<CancellationToken>());
    }
}
