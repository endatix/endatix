using Endatix.Api.Endpoints.DataLists;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists;
using Endatix.Core.UseCases.DataLists.Locales;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.DataLists;

public class AddLocaleTests
{
    private readonly IMediator _mediator;
    private readonly AddLocale _endpoint;

    public AddLocaleTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<AddLocale>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new AddDataListLocaleRequest { DataListId = 1, Locale = "es" };
        _mediator.Send(Arg.Any<AddDataListLocaleCommand>(), Arg.Any<CancellationToken>())
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
        var request = new AddDataListLocaleRequest { DataListId = 1, Locale = "es" };
        _mediator.Send(Arg.Any<AddDataListLocaleCommand>(), Arg.Any<CancellationToken>())
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
        var request = new AddDataListLocaleRequest { DataListId = 1, Locale = "es" };
        DataListDto dto = new(
            1,
            "Cities",
            null,
            DateTime.UtcNow,
            null,
            true,
            0,
            "en",
            ["es"],
            []);
        _mediator.Send(Arg.Any<AddDataListLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<DataListDetailsModel>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(1);
        okResult.Value.AvailableLocales.Should().Equal("es");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new AddDataListLocaleRequest { DataListId = 123, Locale = "fr" };
        DataListDto dto = new(123, "Cities", null, DateTime.UtcNow, null, true, 0, "en", ["fr"], []);
        _mediator.Send(Arg.Any<AddDataListLocaleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<AddDataListLocaleCommand>(cmd =>
                cmd.DataListId == request.DataListId
                && cmd.Locale == request.Locale),
            Arg.Any<CancellationToken>());
    }
}
