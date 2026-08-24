using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.GetById;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class GetByIdTests
{
    private readonly IMediator _mediator;
    private readonly GetById _endpoint;

    public GetByIdTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<GetById>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_PassesIncludeItemsFalseToQuery()
    {
        // Arrange
        GetDataListRequest request = new() { DataListId = 7, IncludeItems = false };
        DataListDto dto = new(
            7,
            "Cities",
            "Major cities",
            DateTime.UtcNow,
            null,
            true,
            2,
            "en",
            ["es"],
            Array.Empty<DataListItemDto>());
        _mediator.Send(Arg.Any<GetDataListByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result.Should().BeOfType<Ok<DataListDetailsModel>>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value.Id.Should().Be(7);
        ok.Value.ItemsCount.Should().Be(2);
        ok.Value.Items.Should().BeEmpty();

        await _mediator.Received(1).Send(
            Arg.Is<GetDataListByIdQuery>(x => x.DataListId == 7 && x.IncludeItems == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DefaultsIncludeItemsToTrue()
    {
        // Arrange
        GetDataListRequest request = new() { DataListId = 3 };
        DataListDto dto = new(
            3,
            "Cities",
            null,
            DateTime.UtcNow,
            null,
            true,
            1,
            "en",
            [],
            [
                new DataListItemDto(
                    10,
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["default"] = "New York" },
                    "NYC",
                    "New York")
            ]);
        _mediator.Send(Arg.Any<GetDataListByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GetDataListByIdQuery>(x => x.DataListId == 3 && x.IncludeItems == true),
            Arg.Any<CancellationToken>());
    }
}
