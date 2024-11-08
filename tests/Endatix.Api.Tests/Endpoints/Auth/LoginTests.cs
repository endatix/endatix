using Endatix.Api.Endpoints.Auth;
using Endatix.Api.Tests.TestExtensions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.Login;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

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
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var okResult = response!.Result.As<Ok<LoginResponse>>();
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.Result.Should().BeAssignableTo<Ok<LoginResponse>>();
        okResult.Should().NotBeNull();
        okResult.Value.Should().NotBeNull();
        okResult.Value?.Email.Should().Be("user@example.com");
        okResult.Value?.AccessToken.Should().Be("valid_access_token");
        okResult.Value?.RefreshToken.Should().Be("valid_refresh_token");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCredentials_ReturnsBadRequestWithValidationFailure()
    {
        // Arrange
        var request = new LoginRequest("wrong.user@example.com", "Password123!");
        var loginCommand = new LoginCommand(request.Email, request.Password);
        var errorLoginResult = Result.Invalid(new ValidationError("The supplied credentials are invalid!"));
        _mediator.Send(loginCommand).Returns(errorLoginResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response!.Result.As<BadRequest<IEnumerable<ValidationError>>>();
        badRequestResult.Should().NotBeNull();
        badRequestResult.Value.Should().Contain(f =>
            f.ErrorMessage == "The supplied credentials are invalid!");
    }
}
