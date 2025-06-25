using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.EmailVerification;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;

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
        result.Value!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(1));
        result.Value!.IsUsed.Should().BeFalse();

        await _tokenRepository.Received(1).AddAsync(Arg.Any<EmailVerificationToken>(), Arg.Any<CancellationToken>());
        await _tokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await _tokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
} 