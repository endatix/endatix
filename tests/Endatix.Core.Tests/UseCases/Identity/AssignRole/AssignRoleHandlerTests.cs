using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.AssignRole;

namespace Endatix.Core.Tests.UseCases.Identity.AssignRole;

public class AssignRoleHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly AssignRoleHandler _handler;

    public AssignRoleHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new AssignRoleHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var command = new AssignRoleCommand(1, "Admin");
        _roleManagementService.AssignRoleToUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Role 'Admin' successfully assigned to user.");

        await _roleManagementService.Received(1).AssignRoleToUserAsync(
            command.UserId,
            command.RoleName,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var command = new AssignRoleCommand(999, "Admin");
        var notFoundResult = Result.NotFound("User with ID 999 not found.");
        _roleManagementService.AssignRoleToUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain("User with ID 999 not found.");
    }

    [Fact]
    public async Task Handle_UserAlreadyHasRole_ReturnsInvalidResult()
    {
        // Arrange
        var command = new AssignRoleCommand(1, "Admin");
        var validationError = new ValidationError
        {
            Identifier = "roleName",
            ErrorMessage = "User already has role 'Admin'."
        };
        var invalidResult = Result.Invalid(validationError);
        _roleManagementService.AssignRoleToUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("User already has role 'Admin'.");
    }

    [Fact]
    public async Task Handle_ServiceError_ReturnsErrorResult()
    {
        // Arrange
        var command = new AssignRoleCommand(1, "Admin");
        var errorResult = Result.Error("An error occurred while assigning the role.");
        _roleManagementService.AssignRoleToUserAsync(command.UserId, command.RoleName, Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("An error occurred while assigning the role.");
    }
}
