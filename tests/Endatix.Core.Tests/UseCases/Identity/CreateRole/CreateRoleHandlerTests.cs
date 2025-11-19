using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.CreateRole;

namespace Endatix.Core.Tests.UseCases.Identity.CreateRole;

public class CreateRoleHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly CreateRoleHandler _handler;

    public CreateRoleHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new CreateRoleHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCreatedResultWithRoleId()
    {
        // Arrange
        var command = new CreateRoleCommand("Manager", "Manager role", new List<string> { "forms.read", "forms.write" });
        var roleId = "123456789";
        _roleManagementService.CreateRoleAsync(
                command.Name,
                command.Description,
                command.Permissions,
                Arg.Any<CancellationToken>())
            .Returns(Result<string>.Created(roleId));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().Be(roleId);

        await _roleManagementService.Received(1).CreateRoleAsync(
            command.Name,
            command.Description,
            command.Permissions,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RoleAlreadyExists_ReturnsInvalidResult()
    {
        // Arrange
        var command = new CreateRoleCommand("Admin", "Admin role", new List<string> { "forms.read" });
        var validationError = new ValidationError
        {
            Identifier = "name",
            ErrorMessage = "Role 'Admin' already exists for this tenant."
        };
        var invalidResult = Result<string>.Invalid(validationError);
        _roleManagementService.CreateRoleAsync(
                command.Name,
                command.Description,
                command.Permissions,
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Role 'Admin' already exists for this tenant.");
    }

    [Fact]
    public async Task Handle_PermissionsNotFound_ReturnsInvalidResult()
    {
        // Arrange
        var command = new CreateRoleCommand("Manager", null, new List<string> { "invalid.permission" });
        var validationError = new ValidationError
        {
            Identifier = "permissionNames",
            ErrorMessage = "The following permissions do not exist: invalid.permission"
        };
        var invalidResult = Result<string>.Invalid(validationError);
        _roleManagementService.CreateRoleAsync(
                command.Name,
                command.Description,
                command.Permissions,
                Arg.Any<CancellationToken>())
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("The following permissions do not exist");
    }

    [Fact]
    public async Task Handle_ServiceError_ReturnsErrorResult()
    {
        // Arrange
        var command = new CreateRoleCommand("Manager", "Manager role", new List<string> { "forms.read" });
        var errorResult = Result<string>.Error("An error occurred while creating the role.");
        _roleManagementService.CreateRoleAsync(
                command.Name,
                command.Description,
                command.Permissions,
                Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError().Should().BeTrue();
        result.Errors.Should().Contain("An error occurred while creating the role.");
    }
}
