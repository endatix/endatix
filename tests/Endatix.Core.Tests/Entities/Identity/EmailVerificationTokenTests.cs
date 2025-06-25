using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Tests.Entities.Identity;

public class EmailVerificationTokenTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesTokenSuccessfully()
    {
        // Arrange
        var userId = 123L;
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // Act
        var verificationToken = new EmailVerificationToken(userId, token, expiresAt);

        // Assert
        verificationToken.Should().NotBeNull();
        verificationToken.UserId.Should().Be(userId);
        verificationToken.Token.Should().Be(token);
        verificationToken.ExpiresAt.Should().Be(expiresAt);
        verificationToken.IsUsed.Should().BeFalse();
        verificationToken.IsExpired.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidUserId_ThrowsArgumentException(long userId)
    {
        // Arrange
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // Act
        var action = () => new EmailVerificationToken(userId, token, expiresAt);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_InvalidToken_ThrowsArgumentException(string token)
    {
        // Arrange
        var userId = 123L;
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // Act
        var action = () => new EmailVerificationToken(userId, token, expiresAt);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ExpiredDate_ThrowsArgumentException()
    {
        // Arrange
        var userId = 123L;
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var action = () => new EmailVerificationToken(userId, token, expiresAt);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsExpired_TokenNotExpired_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var verificationToken = new EmailVerificationToken(userId, token, expiresAt);

        // Act & Assert
        verificationToken.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_TokenExpired_ReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(-1);
        
        // We need to create the token with a valid expiry first, then modify it
        var verificationToken = new EmailVerificationToken(userId, token, DateTime.UtcNow.AddHours(1));
        
        // Use reflection to set the expired date for testing
        var expiresAtProperty = typeof(EmailVerificationToken).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(verificationToken, expiresAt);

        // Act & Assert
        verificationToken.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void MarkAsUsed_TokenNotUsed_MarksAsUsed()
    {
        // Arrange
        var userId = 123L;
        var token = "test-token";
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var verificationToken = new EmailVerificationToken(userId, token, expiresAt);

        // Act
        verificationToken.MarkAsUsed();

        // Assert
        verificationToken.IsUsed.Should().BeTrue();
    }
} 