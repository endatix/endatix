using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class TokenIsExpiredTests
{
    [Fact]
    public void IsExpired_TokenJustCreated_ShouldNotBeExpired()
    {
        // Arrange
        var expiryHours = 1;
        var token = new Token(expiryHours);

        // Act & Assert
        token.Should().NotBeNull();
        token.IsExpired.Should().BeFalse();
    }
}
