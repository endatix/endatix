using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.GetUserById;

namespace Endatix.Core.Tests.UseCases.Identity.GetUserById;

public sealed class GetUserByIdHandlerTests
{
    private readonly IUserService _userService;
    private readonly GetUserByIdHandler _handler;

    public GetUserByIdHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _handler = new GetUserByIdHandler(_userService);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsUser_ReturnsUserWithRoles()
    {
        // Arrange
        const long userId = 1507759960832868352L;
        UserWithRoles user = new()
        {
            Id = userId,
            UserName = "alice",
            Email = "alice@example.com",
            IsVerified = true,
            Roles = ["Admin"]
        };

        _userService
            .GetUserWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(user));

        // Act
        var result = await _handler.Handle(new GetUserByIdQuery(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(user);
        await _userService.Received(1).GetUserWithRolesAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsNotFound_ReturnsNotFound()
    {
        // Arrange
        const long userId = 1507759960832868352L;
        _userService
            .GetUserWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result<UserWithRoles>.NotFound());

        // Act
        var result = await _handler.Handle(new GetUserByIdQuery(userId), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
