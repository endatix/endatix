using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ActivateInvite;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Auth;

public sealed class ActivateInviteTests
{
    private readonly IMediator _mediator;
    private readonly ActivateInvite _endpoint;

    public ActivateInviteTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<ActivateInvite>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInvite_ReturnsOkResult()
    {
        // Arrange
        var request = new ActivateInviteRequest
        {
            Token = "valid-token",
            Password = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var user = new User(123L, "user@example.com", "user@example.com", isVerified: true);

        _mediator.Send(
                Arg.Is<ActivateInviteCommand>(command =>
                    command.Token == request.Token &&
                    command.Password == request.Password),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var okResult = response.Result.As<Ok<ActivateInviteResponse>>();
        okResult.Value!.Success.Should().BeTrue();
        okResult.Value.Message.Should().Be("Invitation activated successfully.");
        okResult.Value.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidInvite_ReturnsProblemResult()
    {
        // Arrange
        var request = new ActivateInviteRequest
        {
            Token = "expired-token",
            Password = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var invalidResult = Result<User>.Invalid(new ValidationError("Invite token has expired"));

        _mediator.Send(
                Arg.Is<ActivateInviteCommand>(command =>
                    command.Token == request.Token &&
                    command.Password == request.Password),
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(400);
        problemResult.ProblemDetails.Detail.Should().Contain("Invite token has expired");
    }
}
