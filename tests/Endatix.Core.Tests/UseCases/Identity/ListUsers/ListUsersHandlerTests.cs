using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity.ListUsers;

namespace Endatix.Core.Tests.UseCases.Identity.ListUsers;

public class ListUsersHandlerTests
{
    private readonly IUserService _userService;
    private readonly ListUsersHandler _handler;

    public ListUsersHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _handler = new ListUsersHandler(_userService);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsSuccess_ReturnsUserWithRoles()
    {
        // Arrange
        var query = new ListUsersQuery(Page: 1, PageSize: 10);
        var usersWithRoles = new List<UserWithRoles>
        {
            new()
            {
                Id = 1,
                UserName = "user1",
                Email = "user1@example.com",
                IsVerified = true,
                Roles = ["Admin"]
            }
        };
        _userService.ListUsersAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<UserWithRoles>>.Success(usersWithRoles));

        // Act
        Result<IEnumerable<UserWithRoles>> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value!.First().Id.Should().Be(1);
        result.Value.First().UserName.Should().Be("user1");
        result.Value.First().Roles.Should().ContainSingle("Admin");

        await _userService.Received(1).ListUsersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsEmptyList_ReturnsEmptyEnumerable()
    {
        // Arrange
        var query = new ListUsersQuery(null, null);
        _userService.ListUsersAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<UserWithRoles>>.Success(new List<UserWithRoles>()));

        // Act
        Result<IEnumerable<UserWithRoles>> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsError_ReturnsErrorResult()
    {
        // Arrange
        var query = new ListUsersQuery(1, 20);
        var errorResult = Result<IReadOnlyList<UserWithRoles>>.Error(
            new ErrorList(["Something failed"], null));
        _userService.ListUsersAsync(Arg.Any<CancellationToken>()).Returns(errorResult);

        // Act
        Result<IEnumerable<UserWithRoles>> result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Something failed");
    }
}
