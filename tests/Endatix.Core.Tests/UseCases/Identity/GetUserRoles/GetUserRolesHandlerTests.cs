using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.GetUserRoles;

namespace Endatix.Core.Tests.UseCases.Identity.GetUserRoles;

public class GetUserRolesHandlerTests
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly GetUserRolesHandler _handler;

    public GetUserRolesHandlerTests()
    {
        _roleManagementService = Substitute.For<IRoleManagementService>();
        _handler = new GetUserRolesHandler(_roleManagementService);
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsSuccessResultWithRoles()
    {
        // Arrange
        var query = new GetUserRolesQuery(1);
        var roles = new List<string> { "Admin", "Panelist" } as IList<string>;
        var successResult = Result.Success(roles);

        _roleManagementService.GetUserRolesAsync(query.UserId, Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain("Admin");
        result.Value.Should().Contain("Panelist");

        await _roleManagementService.Received(1).GetUserRolesAsync(
            query.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidUserId_ReturnsEmptyListWhenNoRoles()
    {
        // Arrange
        var query = new GetUserRolesQuery(1);
        var roles = new List<string>() as IList<string>;
        var successResult = Result.Success(roles);

        _roleManagementService.GetUserRolesAsync(query.UserId, Arg.Any<CancellationToken>())
            .Returns(successResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var query = new GetUserRolesQuery(999);
        var notFoundResult = Result<IList<string>>.NotFound("User with ID 999 not found.");

        _roleManagementService.GetUserRolesAsync(query.UserId, Arg.Any<CancellationToken>())
            .Returns(notFoundResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsNotFound().Should().BeTrue();
        result.Errors.Should().Contain("User with ID 999 not found.");
    }
}
