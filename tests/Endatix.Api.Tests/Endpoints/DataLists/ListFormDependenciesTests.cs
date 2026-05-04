using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.ListFormDependencies;
using Endatix.Core.UseCases.Forms;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class ListFormDependenciesTests
{
    private readonly IMediator _mediator;
    private readonly ListFormDependencies _endpoint;

    public ListFormDependenciesTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ListFormDependencies>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_MapsRequestToQuery()
    {
        ListFormDependenciesRequest request = new() { DataListId = 42 };
        _mediator.Send(Arg.Any<ListFormDependenciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<FormDto>>([]));

        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        await _mediator.Received(1).Send(
            Arg.Is<ListFormDependenciesQuery>(x => x.DataListId == request.DataListId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFormModels()
    {
        ListFormDependenciesRequest request = new() { DataListId = 42 };
        _mediator.Send(Arg.Any<ListFormDependenciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyCollection<FormDto>>(
            [
                new FormDto
                {
                    Id = "11",
                    Name = "Registration",
                    Description = "Main flow",
                    IsEnabled = true,
                    IsPublic = true,
                    CreatedAt = DateTime.UtcNow,
                    SubmissionsCount = 0
                }
            ]));

        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);
        var ok = response.Result.Should().BeOfType<Ok<IEnumerable<FormModel>>>().Subject;
        ok.Value.Should().ContainSingle(x => x.Id == "11" && x.Name == "Registration");
    }
}
