using Endatix.Api.Endpoints.Admin.PlatformAdmins;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Admin.PlatformAdmins;

public class GrantTests
{
    private readonly IMediator _mediator;
    private readonly Grant _endpoint;

    public GrantTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Grant>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var userId = 1L;
        var request = new GrantPlatformAdminRequest { UserId = userId };
        var result = Result<string>.Invalid(new ValidationError("Invalid request"));
        _mediator.Send(Arg.Any<GrantPlatformAdminCommand>(), Arg.Any<CancellationToken>())
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
        var request = new GrantPlatformAdminRequest { UserId = userId };
        var result = Result<string>.NotFound("User not found");
        _mediator.Send(Arg.Any<GrantPlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsOkWithPlatformAdminOperation()
    {
        // Arrange
        var userId = 1L;
        var request = new GrantPlatformAdminRequest { UserId = userId };
        var result = Result<string>.Success("Platform administrator access granted.");
        _mediator.Send(Arg.Any<GrantPlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var okResult = response.Result as Ok<PlatformAdminOperation>;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().NotBeNull();
        okResult.Value!.IsSuccess.Should().BeTrue();
        okResult.Value.Message.Should().Be("Platform administrator access granted.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var userId = 123L;
        var request = new GrantPlatformAdminRequest { UserId = userId };
        var result = Result<string>.Success("Platform administrator access granted.");
        _mediator.Send(Arg.Any<GrantPlatformAdminCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<GrantPlatformAdminCommand>(cmd =>
                cmd.UserId == request.UserId
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
