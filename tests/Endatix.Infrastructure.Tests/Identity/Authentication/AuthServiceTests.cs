using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Endatix.Core.Tests;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Identity.Authentication;

public class AuthServiceTests
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(),
            null, null, null, null, null, null, null, null);
        _passwordHasher = Substitute.For<IPasswordHasher<AppUser>>();
        _authService = new AuthService(_userManager, _passwordHasher);
    }

    [Fact]
    public async Task ValidateCredentials_NullEmail_ThrowsArgumentNullException()
    {
        // Arrange
        string? email = null;
        var password = "password";

        // Act
        var act = () => _authService.ValidateCredentials(email!, password, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("email", ErrorType.Null));
    }

    [Fact]
    public async Task ValidateCredentials_EmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        var email = "";
        var password = "password";

        // Act
        var act = () => _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("email", ErrorType.Empty));
    }

    [Fact]
    public async Task ValidateCredentials_NullPassword_ThrowsArgumentNullException()
    {
        // Arrange
        var email = "email@example.com";
        string? password = null;

        // Act
        var act = () => _authService.ValidateCredentials(email, password!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("password", ErrorType.Null));
    }

    [Fact]
    public async Task ValidateCredentials_EmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var email = "email@example.com";
        var password = "";

        // Act
        var act = () => _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("password", ErrorType.Empty));
    }

    [Fact]
    public async Task ValidateCredentials_UserNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var email = "email@example.com";
        var password = "password";
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        // Act
        var result = await _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain(AuthService.INVALID_CREDENTIALS_ERROR_MESSAGE);
    }

    [Fact]
    public async Task ValidateCredentials_UserEmailNotConfirmed_ReturnsInvalidResult()
    {
        // Arrange
        var email = "email@example.com";
        var password = "password";
        var user = new AppUser { EmailConfirmed = false };
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns(user);

        // Act
        var result = await _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain(AuthService.INVALID_CREDENTIALS_ERROR_MESSAGE);
    }

    [Fact]
    public async Task ValidateCredentials_InvalidPassword_ReturnsInvalidResult()
    {
        // Arrange
        var email = "email@example.com";
        var password = "password";
        var user = new AppUser { EmailConfirmed = true };
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns(user);
        _userManager.CheckPasswordAsync(user, Arg.Any<string>()).Returns(false);

        // Act
        var result = await _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain(AuthService.INVALID_CREDENTIALS_ERROR_MESSAGE);
    }

    [Fact]
    public async Task ValidateCredentials_ValidCredentials_ReturnsSuccessResult()
    {
        // Arrange
        var userId = 1;
        var tenantId = 1;
        var email = "email@example.com";
        var password = "password";
        var user = new AppUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns(user);
        _userManager.CheckPasswordAsync(user, Arg.Any<string>()).Returns(true);

        // Act
        var result = await _authService.ValidateCredentials(email, password, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.UserName.Should().Be(email);
        result.Value.Email.Should().Be(email);
        result.Value.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshToken_InvalidUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = 0;
        var token = "token";

        // Act
        var act = () => _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("userId", ErrorType.ZeroOrNegative));
    }

    [Fact]
    public async Task ValidateRefreshToken_NullToken_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = 1;
        string? token = null;

        // Act
        var act = () => _authService.ValidateRefreshToken(userId, token!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Null));
    }

    [Fact]
    public async Task ValidateRefreshToken_EmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var userId = 1;
        var token = "";

        // Act
        var act = () => _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Empty));
    }

    [Fact]
    public async Task ValidateRefreshToken_UserNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1;
        var token = "token";
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((AppUser?)null);

        // Act
        var result = await _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("Invalid input");
    }

    [Fact]
    public async Task ValidateRefreshToken_InvalidToken_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1;
        var token = "token";
        var user = new AppUser { RefreshTokenHash = "hash", RefreshTokenExpireAt = DateTime.UtcNow.AddDays(1) };
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash, Arg.Any<string>())
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var result = await _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("The supplied refresh token is invalid!");
    }

    [Fact]
    public async Task ValidateRefreshToken_ExpiredToken_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1;
        var token = "token";
        var user = new AppUser { RefreshTokenHash = "hash", RefreshTokenExpireAt = DateTime.UtcNow.AddDays(-1) };
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash, Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result = await _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("The supplied refresh token is invalid!");
    }

    [Fact]
    public async Task ValidateRefreshToken_ValidToken_ReturnsSuccessResult()
    {
        // Arrange
        var userId = 1;
        var tenantId = 1;
        var email = "email@example.com";
        var token = "token";
        var hash = "hash";
        var expireAt = DateTime.UtcNow.AddDays(1);

        var user = new AppUser
        {
            Id = userId,
            TenantId = tenantId,
            UserName = email,
            Email = email,
            RefreshTokenHash = hash,
            RefreshTokenExpireAt = expireAt
        };
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash, Arg.Any<string>())
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result = await _authService.ValidateRefreshToken(userId, token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.UserName.Should().Be(email);
        result.Value.Email.Should().Be(email);
    }

    [Fact]
    public async Task StoreRefreshToken_InvalidUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = 0;
        var token = "token";
        var expireAt = DateTime.UtcNow.AddDays(1);

        // Act
        var act = () => _authService.StoreRefreshToken(userId, token, expireAt, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("userId", ErrorType.ZeroOrNegative));
    }

    [Fact]
    public async Task StoreRefreshToken_NullToken_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = 1;
        string? token = null;
        var expireAt = DateTime.UtcNow.AddDays(1);

        // Act
        var act = () => _authService.StoreRefreshToken(userId, token!, expireAt, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Null));
    }

    [Fact]
    public async Task StoreRefreshToken_EmptyToken_ThrowsArgumentException()
    {
        // Arrange
        var userId = 1;
        var token = "";
        var expireAt = DateTime.UtcNow.AddDays(1);

        // Act
        var act = () => _authService.StoreRefreshToken(userId, token, expireAt, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ErrorMessages.GetErrorMessage("token", ErrorType.Empty));
    }

    [Fact]
    public async Task StoreRefreshToken_UserNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 1;
        var token = "token";
        var expireAt = DateTime.UtcNow.AddDays(1);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((AppUser?)null);

        // Act
        var result = await _authService.StoreRefreshToken(userId, token, expireAt, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors.First().ErrorMessage.Should().Contain("Invalid user");
    }

    [Fact]
    public async Task StoreRefreshToken_ValidInput_ReturnsSuccessResult()
    {
        // Arrange
        var userId = 1;
        var token = "token";
        var expireAt = DateTime.UtcNow.AddDays(1);
        var user = new AppUser();
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(user);
        _passwordHasher.HashPassword(user, Arg.Any<string>()).Returns("hashedToken");

        // Act
        var result = await _authService.StoreRefreshToken(userId, token, expireAt, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userManager.Received(1).UpdateAsync(user);
        user.RefreshTokenHash.Should().Be("hashedToken");
        user.RefreshTokenExpireAt.Should().Be(expireAt);
    }
}
