using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ListLocalesTests
{
    private readonly IMediator _mediator;
    private readonly ListLocales _endpoint;

    public ListLocalesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListLocales>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOkWithLocales()
    {
        IReadOnlyList<string> locales = ["de", "en", "es"];
        _mediator.Send(Arg.Any<ListDistinctDataListLocalesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(locales));

        var response = await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<IReadOnlyList<string>>>().Subject;
        ok.Value.Should().BeEquivalentTo(locales);
    }
}
