using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TokenExtendTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Extend_InvalidExpiryHours_ThrowsArgumentException(int expiryHours)
    {
        // Arrange
        var initialExpiryHours = 1;
        var token = new Token(initialExpiryHours);
        var expectedMessage = ErrorMessages.GetErrorMessage("expiryInHours", ErrorType.ZeroOrNegative);

        // Act
        var action = () => token.Extend(expiryHours);

        // Assert
        action.Should()
            .Throw<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void Extend_ValidExpiryHours_ShouldUpdateExpiryTime()
    {
        // Arrange
        var initialExpiryHours = 1;
        var extensionHours = 24;
        var token = new Token(initialExpiryHours);

        // Act
        token.Extend(extensionHours);

        // Assert
        token.Should().NotBeNull();
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(extensionHours), TimeSpan.FromSeconds(1));
    }
}
