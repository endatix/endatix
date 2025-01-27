using System.Security.Claims;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.MyAccount.ChangePassword;
using MediatR;

namespace Endatix.Core.Tests.UseCases.MyAccount.ChangePassword;

public class ChangePasswordCommandTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        const string currentPassword = "currentPass123";
        const string newPassword = "newPass123";

        // Act
        var command = new ChangePasswordCommand(claimsPrincipal, currentPassword, newPassword);

        // Assert
        command.User.Should().BeSameAs(claimsPrincipal);
        command.CurrentPassword.Should().Be(currentPassword);
        command.NewPassword.Should().Be(newPassword);
    }

    [Fact]
    public void Command_ShouldBeImmutable()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var command = new ChangePasswordCommand(claimsPrincipal, "current", "new");

        // Act & Assert
        command.Should().BeAssignableTo<IRequest<Result<string>>>();
        command.GetType().Should().BeAssignableTo<IEquatable<ChangePasswordCommand>>();
    }
}
