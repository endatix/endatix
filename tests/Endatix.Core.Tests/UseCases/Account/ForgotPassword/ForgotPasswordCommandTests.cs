using Endatix.Core.UseCases.Account.ForgotPassword;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Account.ForgotPassword;

public class ForgotPasswordCommandTests
{
    [Fact]
    public void Constructor_NullOrWhiteSpaceEmail_ThrowsArgumentException()
    {
        // Arrange
        var email = "";

        // Act
        Action act = () => new ForgotPasswordCommand(email);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage(GetErrorMessage(nameof(email), Empty));
    }


    [Fact]
    public void Constructor_ValidParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var command = new ForgotPasswordCommand(email);

        // Assert
        command.Email.Should().Be(email);
    }
}
