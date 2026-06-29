using Endatix.Api.Endpoints.Admin.PlatformAdmins;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Admin.PlatformAdmins;

public class RevokeTests
{
    private readonly IMediator _mediator;
    private readonly Revoke _endpoint;

    public RevokeTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Revoke>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var userId = 1L;
        var request = new RevokePlatformAdminRequest { UserId = userId };
        var result = Result<string>.Invalid(new ValidationError("Invalid request"));
        _mediator.Send(Arg.Any<RevokePlatformAdminCommand>(), Arg.Any<CancellationToken>())
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
        var request = new RevokePlatformAdminRequest { UserId = userId };
        var result = Result<string>.NotFound("User not found");
        _mediator.Send(Arg.Any<RevokePlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_Conflict_ReturnsProblemDetails()
    {
        // Arrange
        var userId = 1L;
        var request = new RevokePlatformAdminRequest { UserId = userId };
        var result = Result<string>.Conflict("Revocation would leave the platform without an active platform administrator.");
        _mediator.Send(Arg.Any<RevokePlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithPlatformAdminOperation()
    {
        // Arrange
        var userId = 1L;
        var request = new RevokePlatformAdminRequest { UserId = userId };
        var result = Result<string>.Success("Platform administrator access revoked.");
        _mediator.Send(Arg.Any<RevokePlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PlatformAdminOperation>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.IsSuccess.Should().BeTrue();
        okResult.Value.Message.Should().Be("Platform administrator access revoked.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var userId = 123L;
        var request = new RevokePlatformAdminRequest { UserId = userId };
        var result = Result<string>.Success("Platform administrator access revoked.");
        _mediator.Send(Arg.Any<RevokePlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<RevokePlatformAdminCommand>(cmd =>
                cmd.UserId == request.UserId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
