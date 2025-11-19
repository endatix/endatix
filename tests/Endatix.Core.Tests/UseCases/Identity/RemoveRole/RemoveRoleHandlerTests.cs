using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.RemoveRole;

namespace Endatix.Core.Tests.UseCases.Identity.RemoveRole;

public class RemoveRoleHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly RemoveRoleHandler _handler;

    public RemoveRoleHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new RemoveRoleHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var command = new RemoveRoleCommand(1, "Admin");
        _roleManagementService.RemoveRoleFromUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Role 'Admin' successfully removed from user.");

        await _roleManagementService.Received(1).RemoveRoleFromUserAsync(
            command.UserId,
            command.RoleName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var command = new RemoveRoleCommand(999, "Admin");
        var notFoundResult = Result.NotFound("User with ID 999 not found.");
        _roleManagementService.RemoveRoleFromUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain("User with ID 999 not found.");
    }

    [Fact]
    public async Task Handle_UserDoesNotHaveRole_ReturnsInvalidResult()
    {
        // Arrange
        var command = new RemoveRoleCommand(1, "Admin");
        var validationError = new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "User does not have role 'Admin'."
        };
        var invalidResult = Result.Invalid(validationError);
        _roleManagementService.RemoveRoleFromUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("User does not have role 'Admin'.");
    }

    [Fact]
    public async Task Handle_ServiceError_ReturnsErrorResult()
    {
        // Arrange
        var command = new RemoveRoleCommand(1, "Admin");
        var errorResult = Result.Error("An error occurred while removing the role.");
        _roleManagementService.RemoveRoleFromUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("An error occurred while removing the role.");
    }
}
