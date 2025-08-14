using Endatix.Api.Endpoints.MyAccount;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using MediatR;
using Errors = Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using FastEndpoints;
using System.Security.Claims;
using Endatix.Core.Abstractions;

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
        var userId = "123";
        
        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Success("Password changed successfully"));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response.Result as Ok<ChangePasswordResponse>;
        okResult.Should().NotBeNull();
        okResult.Value.Message.Should().Be("Password changed successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFailed_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = "123";
        
        _userContext.GetCurrentUserId().Returns(userId);
        _mediator
            .Send(Arg.Any<ChangePasswordCommand>())
            .Returns(Result.Invalid(new ValidationError("Invalid current password")));

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        Console.WriteLine(response.Result);
        var badRequestResult = response.Result as BadRequest<Errors.ProblemDetails>;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCorrectCommandToMediator()
    {
        // Arrange
        var request = new ChangePasswordRequest("currentPass123", "newPass123", "newPass123");
        var userId = "123";
        
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
                    cmd.UserId == long.Parse(userId) &&
                    cmd.CurrentPassword == request.CurrentPassword &&
                    cmd.NewPassword == request.NewPassword),
                Arg.Any<CancellationToken>()
            );
    }
}
