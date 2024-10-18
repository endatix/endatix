using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.RefreshToken;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Endatix.Core.Tests.UseCases.Identity.RefreshToken;

public class RefreshTokenHandlerTests
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _tokenService = Substitute.For<ITokenService>();
        _handler = new RefreshTokenHandler(_authService, _tokenService);
    }

    [Fact]
    public async Task Handle_InvalidAccessToken_ReturnsInvalidResult()
    {
        // Arrange
        var command = new RefreshTokenCommand("invalid_access_token", "refresh_token");
        var validationErrors = new List<ValidationError> { new("Invalid access token") };
        _tokenService.ValidateAccessTokenAsync(command.AccessToken, false)
            .Returns(Task.FromResult<Result<long>>(Result.Invalid(validationErrors)));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public async Task Handle_AccessTokenValidationError_ReturnsErrorResult()
    {
        // Arrange
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        _tokenService.ValidateAccessTokenAsync(command.AccessToken, false)
            .Returns(Task.FromResult<Result<long>>(Result.Error()));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidRefreshToken_ReturnsInvalidResult()
    {
        // Arrange
        var command = new RefreshTokenCommand("access_token", "invalid_refresh_token");
        var userId = 1;
        var validationErrors = new List<ValidationError> { new("Invalid refresh token") };

        _tokenService.ValidateAccessTokenAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success((long)userId)));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(validationErrors));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().BeEquivalentTo(validationErrors);
    }

    [Fact]
    public async Task Handle_RefreshTokenValidationError_ReturnsErrorResult()
    {
        // Arrange
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1;

        _tokenService.ValidateAccessTokenAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success((long)userId)));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Error());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidTokens_ReturnsSuccessResultWithNewTokens()
    {
        // Arrange
        var command = new RefreshTokenCommand("access_token", "refresh_token");
        var userId = 1;
        var user = new User(userId, "testuser", "test@example.com", true);
        var newAccessToken = new TokenDto("new_access_token", DateTime.UtcNow.AddMinutes(15));
        var newRefreshToken = new TokenDto("new_refresh_token", DateTime.UtcNow.AddDays(7));

        _tokenService.ValidateAccessTokenAsync(command.AccessToken, false)
            .Returns(Task.FromResult(Result.Success((long)userId)));
        _authService.ValidateRefreshToken(userId, command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result<User>.Success(user));
        _tokenService.IssueAccessToken(user).Returns(newAccessToken);
        _tokenService.IssueRefreshToken().Returns(newRefreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be(newRefreshToken);

        await _authService.Received(1).StoreRefreshToken(user.Id, newRefreshToken.Token, newRefreshToken.ExpireAt, Arg.Any<CancellationToken>());
    }
}
