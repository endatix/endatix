using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Locales;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class SetDefaultLocaleTests
{
    private readonly IMediator _mediator;
    private readonly SetDefaultLocale _endpoint;

    public SetDefaultLocaleTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<SetDefaultLocale>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new SetDataListDefaultLocaleRequest { DataListId = 1, DefaultLocale = "en" };
        _mediator.Send(Arg.Any<SetDataListDefaultLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid());

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_DataListNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var request = new SetDataListDefaultLocaleRequest { DataListId = 1, DefaultLocale = "en" };
        _mediator.Send(Arg.Any<SetDataListDefaultLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Data list not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithDetails()
    {
        // Arrange
        var request = new SetDataListDefaultLocaleRequest { DataListId = 1, DefaultLocale = "fr" };
        DataListDto dto = new(
            1,
            "Cities",
            null,
            DateTime.UtcNow,
            null,
            true,
            0,
            "fr",
            ["es"],
            []);
        _mediator.Send(Arg.Any<SetDataListDefaultLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<DataListDetailsModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(1);
        okResult.Value.DefaultLocale.Should().Be("fr");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new SetDataListDefaultLocaleRequest { DataListId = 123, DefaultLocale = "de" };
        DataListDto dto = new(123, "Cities", null, DateTime.UtcNow, null, true, 0, "de", [], []);
        _mediator.Send(Arg.Any<SetDataListDefaultLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<SetDataListDefaultLocaleCommand>(cmd =>
                cmd.DataListId == request.DataListId
                && cmd.DefaultLocale == request.DefaultLocale),
            Arg.Any<CancellationToken>());
    }
}
