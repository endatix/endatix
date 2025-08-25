using Endatix.Api.Endpoints.MyAccount;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using FastEndpoints;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Api.Tests.Endpoints.MyAccount;

public class ChangePasswordTests
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;
    private readonly ChangePassword _endpoint;

    public ChangePasswordTests()
    {
        _mediator = Substitute.For<IMediator>();
        _userContext = Substitute.For<IUserContext>();
        _endpoint = Factory.Create<ChangePassword>(ctx =>
        {
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test-user") }));
        }, _mediator, _userContext);
    }


    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ShouldReturnOkResult()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = 123L;

        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Success("Password changed successfully"));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<ChangePasswordResponse>;
        Assert.NotNull(okResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("Password changed successfully", okResult.Value.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidResult_ShouldReturnProblemDetails()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = 123L;

        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Invalid(new ValidationError("Invalid current password")));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemDetailsResult = response.Result as ProblemHttpResult;

        Assert.NotNull(problemDetailsResult);
        Assert.NotNull(problemDetailsResult.ProblemDetails);

        problemDetailsResult.ProblemDetails.Title.Should().Be("There was a problem with your request.");
        problemDetailsResult.ProblemDetails.Detail.Should().Contain("Invalid current password");
        problemDetailsResult.ProblemDetails.Status.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_WhenErrorResult_ShouldReturnProblemDetails()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = 123L;

        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Error("Failed to change password"));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var problemDetailsResult = response.Result as ProblemHttpResult;

        Assert.NotNull(problemDetailsResult);
        Assert.NotNull(problemDetailsResult.ProblemDetails);
        problemDetailsResult.ProblemDetails.Title.Should().Be("An unexpected error occurred.");
        problemDetailsResult.ProblemDetails.Detail.Should().Contain("Failed to change password");
        problemDetailsResult.ProblemDetails.Status.Should().Be(500);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectCommandToMediator()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = 123L;

        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Success("Success"));

        // Act
        await _endpoint.ExecuteAsync(request, default);

        // Assert
        await _mediator
            .Received(1)
            .Send(
                Arg.Is<ChangePasswordCommand>(cmd =>
                    cmd.UserId == userId &&
                    cmd.CurrentPassword == request.CurrentPassword &&
                    cmd.NewPassword == request.NewPassword),
                Arg.Any<CancellationToken>()
            );
    }
}
