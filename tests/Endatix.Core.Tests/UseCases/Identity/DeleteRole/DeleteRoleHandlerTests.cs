using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.DeleteRole;

namespace Endatix.Core.Tests.UseCases.Identity.DeleteRole;

public class DeleteRoleHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly DeleteRoleHandler _handler;

    public DeleteRoleHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new DeleteRoleHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResultWithMessage()
    {
        // Arrange
        var command = new DeleteRoleCommand("Manager");
        _roleManagementService.DeleteRoleAsync(
                command.RoleName,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Role 'Manager' successfully deleted.");

        await _roleManagementService.Received(1).DeleteRoleAsync(
            command.RoleName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RoleNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var command = new DeleteRoleCommand("NonExistent");
        var notFoundResult = Result.NotFound("Role 'NonExistent' not found for this tenant.");
        _roleManagementService.DeleteRoleAsync(
                command.RoleName,
                Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain("Role 'NonExistent' not found for this tenant.");
    }

    [Fact]
    public async Task Handle_SystemDefinedRole_ReturnsInvalidResult()
    {
        // Arrange
        var command = new DeleteRoleCommand("Admin");
        var validationError = new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "Cannot delete system-defined role 'Admin'."
        };
        var invalidResult = Result.Invalid(validationError);
        _roleManagementService.DeleteRoleAsync(
                command.RoleName,
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Cannot delete system-defined role 'Admin'.");
    }

    [Fact]
    public async Task Handle_RoleAssignedToUsers_ReturnsInvalidResult()
    {
        // Arrange
        var command = new DeleteRoleCommand("Manager");
        var validationError = new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "Cannot delete role 'Manager' because it is assigned to one or more users."
        };
        var invalidResult = Result.Invalid(validationError);
        _roleManagementService.DeleteRoleAsync(
                command.RoleName,
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Cannot delete role 'Manager' because it is assigned to one or more users.");
    }

    [Fact]
    public async Task Handle_ServiceError_ReturnsErrorResult()
    {
        // Arrange
        var command = new DeleteRoleCommand("Manager");
        var errorResult = Result.Error("An error occurred while deleting the role.");
        _roleManagementService.DeleteRoleAsync(
                command.RoleName,
                Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("An error occurred while deleting the role.");
    }
}
