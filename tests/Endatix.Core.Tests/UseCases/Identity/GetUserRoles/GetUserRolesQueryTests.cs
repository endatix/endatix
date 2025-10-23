using Endatix.Core.UseCases.Identity.GetUserRoles;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Identity.GetUserRoles;

public class GetUserRolesQueryTests
{
    [Fact]
    public void Constructor_NegativeOrZeroUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = -1L;

        // Act
        Action act = () => new GetUserRolesQuery(userId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(userId), ZeroOrNegative));
    }

    [Fact]
    public void Constructor_ValidUserId_SetsPropertyCorrectly()
    {
        // Arrange
        var userId = 1L;

        // Act
        var query = new GetUserRolesQuery(userId);

        // Assert
        query.UserId.Should().Be(userId);
    }
}
