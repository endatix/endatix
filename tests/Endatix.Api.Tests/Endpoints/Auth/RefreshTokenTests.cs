using FastEndpoints;
using MediatR;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.RefreshToken;
using Endatix.Api.Tests.TestExtensions;

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
    public async Task HandleAsync_ValidTokens_ReturnsOkWithNewTokens()
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
        await _endpoint.HandleAsync(request, default);
        var response = _endpoint.Response;

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(200);
        response.Should().NotBeNull();
        response.AccessToken.Should().Be("new_access_token");
        response.RefreshToken.Should().Be("new_refresh_token");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCredentials_ThrowsValidationFailureException()
    {
        // Arrange
        var request = new RefreshTokenRequest("Bearer wrong_access_token", "wrong_refresh_token");
        var refreshTokenCommand = new RefreshTokenCommand("wrong_access_token", "wrong_refresh_token");
        var errorRefreshTokenResult = Result.Invalid();
        _mediator.Send(refreshTokenCommand).Returns(errorRefreshTokenResult);

        // Act
        Func<Task> act = async () => await _endpoint.HandleAsync(request, default);

        // Assert
        var expectedErrorMessage = "Invalid or expired token.";
        await act.Should().ThrowValidationFailureAsync(expectedErrorMessage);
        _endpoint.ValidationFailed.Should().BeTrue();
        _endpoint.ValidationFailures.Should().Contain(f =>
            f.ErrorMessage == expectedErrorMessage);
    }
}
