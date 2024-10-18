using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.TestExtensions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.Login;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class LoginTests
{
    private readonly IMediator _mediator;
    private readonly Login _endpoint;

    public LoginTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Login>(_mediator);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsOkResult()
    {
        // Arrange
        var request = new LoginRequest("user@example.com", "Password123!");
        var loginCommand = new LoginCommand(request.Email, request.Password);

        var authTokens = new AuthTokensDto(
            new TokenDto("valid_access_token", DateTime.UtcNow.AddHours(1)),
            new TokenDto("valid_refresh_token", DateTime.UtcNow.AddDays(1)));
        var successLoginResult = Result.Success(authTokens);

        _mediator.Send(loginCommand)
           .Returns(successLoginResult);

        // Act
        await _endpoint.HandleAsync(request, default);
        var response = _endpoint.Response;

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response!.Email.Should().Be("user@example.com");
        response.AccessToken.Should().Be("valid_access_token");
        response.RefreshToken.Should().Be("valid_refresh_token");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCredentials_ThrowsValidationFailureException()
    {
        // Arrange
        var request = new LoginRequest("wrong.user@example.com", "Password123!");
        var loginCommand = new LoginCommand(request.Email, request.Password);
        var errorLoginResult = Result.Invalid();
        _mediator.Send(loginCommand).Returns(errorLoginResult);

        // Act
        Func<Task> act = async () => await _endpoint.HandleAsync(request, default);

        // Assert
        var expectedErrorMessage = "The supplied credentials are invalid!";
        await act.Should().ThrowValidationFailureAsync(expectedErrorMessage);
        _endpoint.ValidationFailed.Should().BeTrue();
        _endpoint.ValidationFailures.Should().Contain(f =>
            f.ErrorMessage == expectedErrorMessage);
    }
}
