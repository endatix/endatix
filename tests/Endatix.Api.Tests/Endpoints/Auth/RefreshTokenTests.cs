using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Errors = Microsoft.AspNetCore.Mvc;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.RefreshToken;

namespace Endatix.Api.Tests.Endpoints.Auth;

public class RefreshTokenTests
{
    private readonly IMediator _mediator;
    private readonly RefreshToken _endpoint;

    public RefreshTokenTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<RefreshToken>(_mediator);
    }

    [Fact]
    public async Task ExecuteAsync_ValidTokens_ReturnsOkWithNewTokens()
    {
        // Arrange
        var request = new RefreshTokenRequest("Bearer old_access_token", "old_refresh_token");
        var refreshTokenCommand = new RefreshTokenCommand("old_access_token", "old_refresh_token");

        var authTokens = new AuthTokensDto(
            new TokenDto("new_access_token", DateTime.UtcNow.AddHours(1)),
            new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(1)));
        var successRefreshTokenResult = Result.Success(authTokens);

        _mediator.Send(refreshTokenCommand)
           .Returns(successRefreshTokenResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        var okResult = response?.Result as Ok<RefreshTokenResponse>;
        okResult?.Value.Should().NotBeNull();
        okResult?.Value?.AccessToken.Should().Be("new_access_token");
        okResult?.Value?.RefreshToken.Should().Be("new_refresh_token");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCredentials_ThrowsValidationFailureException()
    {
        // Arrange
        var request = new RefreshTokenRequest("Bearer wrong_access_token", "wrong_refresh_token");
        var refreshTokenCommand = new RefreshTokenCommand("wrong_access_token", "wrong_refresh_token");
        var errorRefreshTokenResult = Result.Invalid();
        _mediator.Send(refreshTokenCommand).Returns(errorRefreshTokenResult);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badResult = response?.Result as BadRequest<Errors.ProblemDetails>;
        badResult.Should().NotBeNull();
        badResult?.StatusCode.Should().Be(400);
        badResult?.Value?.Title.Should().Be("Invalid or expired token.");
    }
}
