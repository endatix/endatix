using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TokenConstructorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidExpiryHours_ThrowsArgumentException(int expiryHours)
    {
        // Arrange
        var expectedMessage = ErrorMessages.GetErrorMessage("expiryInHours", ErrorType.ZeroOrNegative);

        // Act
        var action = () => new Token(expiryHours);

        // Assert
        action.Should()
            .Throw<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void Constructor_ValidExpiryHours_ShouldCreateToken()
    {
        // Arrange
        var expiryHours = 24;

        // Act
        var token = new Token(expiryHours);

        // Assert
        token.Should().NotBeNull();
        token.Value.Should().NotBeNullOrEmpty();
        token.Value.Length.Should().Be(64); // 32 bytes in hex = 64 characters
        token.IsExpired.Should().BeFalse();
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(expiryHours), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_MultipleCalls_ShouldCreateDifferentTokens()
    {
        // Arrange
        var expiryHours = 1;
        
        // Act
        var token1 = new Token(expiryHours);
        var token2 = new Token(expiryHours);

        // Assert
        token1.Should().NotBeNull();
        token2.Should().NotBeNull();
        token1.Value.Should().NotBe(token2.Value);
    }
} 
