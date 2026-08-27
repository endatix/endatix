using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Api.Endpoints.Themes;
using Endatix.Core.UseCases.Themes.Delete;

namespace Endatix.Api.Tests.Endpoints.Themes;

public class DeleteTests
{
    private readonly IMediator _mediator;
    private readonly Delete _endpoint;

    public DeleteTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Delete>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var request = new DeleteRequest { ThemeId = 1 };
        Result<string> result = Result.NotFound();
        var deleteThemeCommand = new DeleteThemeCommand(request.ThemeId);

        _mediator.Send(Arg.Is(deleteThemeCommand), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new DeleteRequest { ThemeId = 0 }; // Invalid ID
        Result<string> result = Result.Invalid();

        _mediator.Send(Arg.Any<DeleteThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulDelete_ReturnsNoContent()
    {
        // Arrange
        var request = new DeleteRequest { ThemeId = 1 };
        var result = Result.Success("1");

        _mediator.Send(Arg.Any<DeleteThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<string>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().Be("1");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var request = new DeleteRequest { ThemeId = 1 };
        var result = Result.Success("1");

        _mediator.Send(Arg.Any<DeleteThemeCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<DeleteThemeCommand>(cmd => cmd.ThemeId == request.ThemeId),
            Arg.Any<CancellationToken>()
        );
    }
}