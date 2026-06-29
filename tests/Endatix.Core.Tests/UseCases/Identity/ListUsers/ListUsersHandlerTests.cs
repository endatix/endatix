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
        var query = new ListUsersQuery(
            page: 2,
            pageSize: 10,
            search: " user1 ",
            role: "Admin",
            status: "ACTIVE");
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
        var pagedUsers = new Paged<UserWithRoles>(2, 10, 11, 2, usersWithRoles);
        _userService
            .ListUsersAsync(
                10,
                10,
                "user1",
                "Admin",
                "active",
                Arg.Any<CancellationToken>())
            .Returns(Result<Paged<UserWithRoles>>.Success(pagedUsers));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(2);
        result.Value.TotalRecords.Should().Be(11);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Id.Should().Be(1);
        result.Value.Items.First().UserName.Should().Be("user1");
        result.Value.Items.First().Roles.Should().ContainSingle("Admin");

        await _userService
            .Received(1)
            .ListUsersAsync(10, 10, "user1", "Admin", "active", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsEmptyList_ReturnsEmptyEnumerable()
    {
        // Arrange
        var query = new ListUsersQuery(null, null, null, null, null);
        var pagedUsers = Paged<UserWithRoles>.Empty(ListUsersQuery.DefaultPageSize);
        _userService
            .ListUsersAsync(
                0,
                ListUsersQuery.DefaultPageSize,
                null,
                null,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<Paged<UserWithRoles>>.Success(pagedUsers));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsError_ReturnsErrorResult()
    {
        // Arrange
        var query = new ListUsersQuery(1, 20, null, null, null);
        var errorResult = Result<Paged<UserWithRoles>>.Error(
            new ErrorList(["Something failed"], null));
        _userService
            .ListUsersAsync(Arg.Any<int>(), Arg.Any<int>(), null, null, null, Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Something failed");
    }
}
