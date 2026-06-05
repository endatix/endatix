using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ReplaceUserRoles;

namespace Endatix.Core.Tests.UseCases.Identity.ReplaceUserRoles;

public class ReplaceUserRolesHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly ReplaceUserRolesHandler _handler;

    public ReplaceUserRolesHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new ReplaceUserRolesHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ReplaceUserRolesCommand(1, ["Admin", "Creator"]);
        _roleManagementService
            .ReplaceRolesForUserAsync(command.UserId, command.RoleNames, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("User roles updated.");
        await _roleManagementService.Received(1).ReplaceRolesForUserAsync(
            command.UserId,
            command.RoleNames,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ServiceFailure_ReturnsErrorResult()
    {
        // Arrange
        var command = new ReplaceUserRolesCommand(1, ["MissingRole"]);
        var invalidResult = Result.Invalid(new ValidationError
        {
            Identifier = "roleNames",
            ErrorMessage = "The following roles do not exist: MissingRole"
        });
        _roleManagementService
            .ReplaceRolesForUserAsync(command.UserId, command.RoleNames, TestContext.Current.CancellationToken)
            .Returns(invalidResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsInvalid().Should().BeTrue();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The following roles do not exist: MissingRole");
    }
}
