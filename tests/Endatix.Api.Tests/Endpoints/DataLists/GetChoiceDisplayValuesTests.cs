using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class GetChoiceDisplayValuesTests
{
    private readonly IMediator _mediator;
    private readonly GetChoiceDisplayValues _endpoint;

    public GetChoiceDisplayValuesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetChoiceDisplayValues>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        GetChoiceDisplayValuesRequest request = new() { DataListId = 11, Values = ["a", "b"] };
        _mediator.Send(Arg.Any<GetDataListChoiceDisplayValuesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DataListChoiceDisplayValueDto>>([]));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListChoiceDisplayValuesQuery>(x =>
                x.DataListId == request.DataListId &&
                x.Values.SequenceEqual(request.Values)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPublicChoiceModels()
    {
        GetChoiceDisplayValuesRequest request = new() { DataListId = 11, Values = ["1"] };
        _mediator.Send(Arg.Any<GetDataListChoiceDisplayValuesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<DataListChoiceDisplayValueDto>>(
                [new DataListChoiceDisplayValueDto("1", "United States")]));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>>().Subject;
        ok.Value.Should().ContainSingle(x => x.Value == "1" && x.Label == "United States");
    }
}
