using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.UnlockUser;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public class UnlockUserTests
{
    private readonly IMediator _mediator;
    private readonly UnlockUser _endpoint;

    public UnlockUserTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<UnlockUser>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var userId = 1L;
        var request = new UserIdRequest { UserId = userId };
        var result = Result.Invalid();
        _mediator.Send(Arg.Any<UnlockUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsProblemDetails()
    {
        // Arrange
        var userId = 1L;
        var request = new UserIdRequest { UserId = userId };
        var result = Result.NotFound("User not found");
        _mediator.Send(Arg.Any<UnlockUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithUserOperation()
    {
        // Arrange
        var userId = 1L;
        var request = new UserIdRequest { UserId = userId };
        var result = Result.Success();
        _mediator.Send(Arg.Any<UnlockUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<UserOperation>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.IsSuccess.Should().BeTrue();
        okResult.Value.Message.Should().Be("User unlocked.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var userId = 123L;
        var request = new UserIdRequest { UserId = userId };
        var result = Result.Success();
        _mediator.Send(Arg.Any<UnlockUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);
        
        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<UnlockUserCommand>(cmd =>
                cmd.UserId == request.UserId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
