using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.RemoveUserAccess;

namespace Endatix.Core.Tests.UseCases.Identity.RemoveUserAccess;

public sealed class RemoveUserAccessHandlerTests
{
    private readonly IUserService _userService;
    private readonly RemoveUserAccessHandler _handler;

    public RemoveUserAccessHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _handler = new RemoveUserAccessHandler(_userService);
    }

    [Fact]
    public async Task Handle_WithValidUser_DelegatesToUserService()
    {
        // Arrange
        var command = new RemoveUserAccessCommand(123L);
        _userService.RemoveUserAccessAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userService.Received(1).RemoveUserAccessAsync(
            command.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserServiceFails_ReturnsFailure()
    {
        // Arrange
        var command = new RemoveUserAccessCommand(123L);
        _userService.RemoveUserAccessAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(Result.NotFound());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
