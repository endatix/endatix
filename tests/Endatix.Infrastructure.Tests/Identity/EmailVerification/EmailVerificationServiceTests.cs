using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.EmailVerification;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Identity.EmailVerification;

public class EmailVerificationServiceTests
{
    private readonly IRepository<EmailVerificationToken> _tokenRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly EmailVerificationOptions _options;
    private readonly EmailVerificationService _sut;

    public EmailVerificationServiceTests()
    {
        _tokenRepository = Substitute.For<IRepository<EmailVerificationToken>>();
        _userManager = Substitute.For<UserManager<AppUser>>(
            Substitute.For<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
        _options = new EmailVerificationOptions { TokenExpiryInHours = 24 };
        var optionsWrapper = Substitute.For<IOptions<EmailVerificationOptions>>();
        optionsWrapper.Value.Returns(_options);
        _sut = new EmailVerificationService(_tokenRepository, _userManager, optionsWrapper);
    }

    [Fact]
    public async Task CreateVerificationTokenAsync_ValidUserId_CreatesTokenSuccessfully()
    {
        // Arrange
        var userId = 123L;
        var user = new AppUser { Id = userId, EmailConfirmed = false };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _tokenRepository.ListAsync(Arg.Any<EmailVerificationTokenByUserIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmailVerificationToken>());

        // Act
        var result = await _sut.CreateVerificationTokenAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(userId);
        result.Value!.Token.Should().NotBeNullOrEmpty();
        result.Value!.RawToken.Should().NotBeNullOrEmpty();
        result.Value!.Token.Should().Be(EmailVerificationToken.HashToken(result.Value.RawToken!));
        result.Value!.Token.Should().NotBe(result.Value.RawToken);
        result.Value!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(1));
        result.Value!.IsUsed.Should().BeFalse();

        await _tokenRepository.Received(1).AddAsync(Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateVerificationTokenAsync_UserAlreadyVerified_ReturnsInvalidResult()
    {
        // Arrange
        var userId = 123L;
        var user = new AppUser { Id = userId, EmailConfirmed = true };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        // Act
        var result = await _sut.CreateVerificationTokenAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(ve => ve.ErrorMessage == "User is already verified");
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_VerifiesUserAndMarksTokenAsUsed()
    {
        // Arrange
        var token = "valid-token";
        var userId = 123L;
        var user = new AppUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            EmailConfirmed = false
        };
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        user.EmailConfirmed.Should().BeTrue();
        verificationToken.IsUsed.Should().BeTrue();

        _userManager.Received(1).UpdateAsync(user);
        await _tokenRepository.Received(1).UpdateAsync(verificationToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsInvalidResult()
    {
        // Arrange
        var token = "expired-token";
        var userId = 123L;
        // Create token with valid expiry first, then modify it to be expired
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));

        // Use reflection to set the expired date for testing
        var expiresAtProperty = typeof(EmailVerificationToken).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(verificationToken, DateTime.UtcNow.AddHours(-1));

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);

        // Act
        var result = await _sut.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(ve => ve.ErrorMessage == "Verification token has expired");
    }

    [Fact]
    public async Task VerifyEmailAsync_TokenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var token = "not-found-token";

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns((EmailVerificationToken?)null);

        // Act
        var result = await _sut.VerifyEmailAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ActivateInviteAsync_ValidToken_SetsPasswordConfirmsEmailAndMarksTokenAsUsed()
    {
        // Arrange
        var token = "valid-invite-token";
        var resetToken = "identity-reset-token";
        var newPassword = "NewPassword123!";
        var userId = 123L;
        var user = new AppUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            EmailConfirmed = false
        };
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns(resetToken);
        _userManager.ResetPasswordAsync(user, resetToken, newPassword).Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _sut.ActivateInviteAsync(token, newPassword, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be(user.Email);
        user.EmailConfirmed.Should().BeTrue();
        verificationToken.IsUsed.Should().BeTrue();

        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);
        await _userManager.Received(1).ResetPasswordAsync(user, resetToken, newPassword);
        _userManager.Received(1).UpdateAsync(user);
        await _tokenRepository.Received(1).UpdateAsync(verificationToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateInviteAsync_TokenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var token = "missing-token";

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns((EmailVerificationToken?)null);

        // Act
        var result = await _sut.ActivateInviteAsync(token, "NewPassword123!", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ActivateInviteAsync_ExpiredToken_ReturnsInvalidResult()
    {
        // Arrange
        var token = "expired-invite-token";
        var userId = 123L;
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));
        var expiresAtProperty = typeof(EmailVerificationToken).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(verificationToken, DateTime.UtcNow.AddHours(-1));

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);

        // Act
        var result = await _sut.ActivateInviteAsync(token, "NewPassword123!", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(ve => ve.ErrorMessage == "Invite token has expired");
    }

    [Fact]
    public async Task ActivateInviteAsync_UsedToken_ReturnsInvalidResult()
    {
        // Arrange
        var token = "used-invite-token";
        var userId = 123L;
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));
        verificationToken.MarkAsUsed();

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);

        // Act
        var result = await _sut.ActivateInviteAsync(token, "NewPassword123!", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle(ve => ve.ErrorMessage == "Invite token has already been used");
    }

    [Fact]
    public async Task GetPendingInviteUserAsync_ValidToken_ReturnsUserWithoutUsingToken()
    {
        // Arrange
        var token = "valid-invite-token";
        var userId = 123L;
        var user = new AppUser
        {
            Id = userId,
            UserName = "test@example.com",
            Email = "test@example.com",
            EmailConfirmed = false
        };
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));

        _tokenRepository.FirstOrDefaultAsync(Arg.Any<EmailVerificationTokenByTokenSpec>(), Arg.Any<CancellationToken>())
            .Returns(verificationToken);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        // Act
        var result = await _sut.GetPendingInviteUserAsync(token, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("test@example.com");
        verificationToken.IsUsed.Should().BeFalse();

        await _tokenRepository.DidNotReceive().UpdateAsync(Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateVerificationTokensAsync_UnusedTokens_MarksTokensAsUsedWithSingleRangeUpdate()
    {
        // Arrange
        var userId = 123L;
        var unusedToken1 = new EmailVerificationToken(userId, "token-1", DateTime.UtcNow.AddHours(1));
        var unusedToken2 = new EmailVerificationToken(userId, "token-2", DateTime.UtcNow.AddHours(1));
        var usedToken = new EmailVerificationToken(userId, "token-3", DateTime.UtcNow.AddHours(1));
        usedToken.MarkAsUsed();
        _tokenRepository.ListAsync(
                Arg.Any<EmailVerificationTokenByUserIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns([unusedToken1, unusedToken2, usedToken]);

        // Act
        var result = await _sut.InvalidateVerificationTokensAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        unusedToken1.IsUsed.Should().BeTrue();
        unusedToken2.IsUsed.Should().BeTrue();
        await _tokenRepository.Received(1).UpdateRangeAsync(
            Arg.Is<IEnumerable<EmailVerificationToken>>(tokens =>
                tokens.SequenceEqual(new[] { unusedToken1, unusedToken2 })),
            Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceive().UpdateAsync(
            Arg.Any<EmailVerificationToken>(),
            Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateVerificationTokensAsync_NoUnusedTokens_DoesNotUpdateRepository()
    {
        // Arrange
        var userId = 123L;
        var usedToken = new EmailVerificationToken(userId, "token-1", DateTime.UtcNow.AddHours(1));
        usedToken.MarkAsUsed();
        _tokenRepository.ListAsync(
                Arg.Any<EmailVerificationTokenByUserIdSpec>(),
                Arg.Any<CancellationToken>())
            .Returns([usedToken]);

        // Act
        var result = await _sut.InvalidateVerificationTokensAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _tokenRepository.DidNotReceive().UpdateRangeAsync(
            Arg.Any<IEnumerable<EmailVerificationToken>>(),
            Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}