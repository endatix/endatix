using Endatix.Api.Endpoints.Users;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.RemoveUserAccess;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Users;

public sealed class RemoveUserAccessTests
{
    private readonly IMediator _mediator;
    private readonly RemoveUserAccess _endpoint;

    public RemoveUserAccessTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<RemoveUserAccess>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRemovalSucceeds_ReturnsOkResult()
    {
        // Arrange
        var request = new UserIdRequest { UserId = 123L };

        _mediator.Send(Arg.Any<RemoveUserAccessCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<UserOperation>>();
        okResult.Should().NotBeNull();
        okResult.Value!.IsSuccess.Should().BeTrue();
        okResult.Value.Message.Should().Be("User access removed.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotFound_ReturnsProblemResult()
    {
        // Arrange
        var request = new UserIdRequest { UserId = 123L };

        _mediator.Send(Arg.Any<RemoveUserAccessCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("User not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(404);
    }
}
