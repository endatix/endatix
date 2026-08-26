using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Paging;
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

    private static ListUsersQuery Query(
        int? page = null,
        int? pageSize = null,
        string? search = null,
        string? role = null,
        string? status = null) =>
        new(
            new SearchablePageRequest(page, pageSize, search),
            new UserListCriteria(role, status));

    [Fact]
    public async Task Handle_WhenServiceReturnsSuccess_ReturnsUserWithRoles()
    {
        // Arrange
        var query = Query(page: 2, pageSize: 10, search: " user1 ", role: "Admin", status: "ACTIVE");
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
                Arg.Any<SearchablePageRequest>(),
                Arg.Any<UserListCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<Paged<UserWithRoles>>.Success(pagedUsers));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Page.Should().Be(2);
        result.Value.TotalRecords.Should().Be(11);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.First().UserName.Should().Be("user1");
        result.Value.Items.First().Roles.Should().ContainSingle("Admin");
    }

    [Fact]
    public async Task Handle_PassesNormalizedPagingAndCriteriaThrough()
    {
        // Arrange
        var query = Query(page: 2, pageSize: 10, search: " user1 ", role: " Admin ", status: "ACTIVE");
        _userService
            .ListUsersAsync(
                Arg.Any<SearchablePageRequest>(),
                Arg.Any<UserListCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<Paged<UserWithRoles>>.Success(Paged<UserWithRoles>.Empty(10)));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        await _userService.Received(1).ListUsersAsync(
            Arg.Is<SearchablePageRequest>(p =>
                p.Paging.Page == 2 &&
                p.Paging.PageSize == 10 &&
                p.Paging.Skip == 10 &&
                p.Search == "user1"),
            Arg.Is<UserListCriteria>(c =>
                c.Role == "Admin" &&
                c.Status == "active" &&
                c.Sort == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsEmptyList_ReturnsEmptyEnumerable()
    {
        // Arrange
        var query = Query();
        var pagedUsers = Paged<UserWithRoles>.Empty(PagedRequestLimits.DEFAULT_PAGE_SIZE);
        _userService
            .ListUsersAsync(
                Arg.Any<SearchablePageRequest>(),
                Arg.Any<UserListCriteria>(),
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
        var query = Query(page: 1, pageSize: 20);
        var errorResult = Result<Paged<UserWithRoles>>.Error(
            new ErrorList(["Something failed"], null));
        _userService
            .ListUsersAsync(
                Arg.Any<SearchablePageRequest>(),
                Arg.Any<UserListCriteria>(),
                Arg.Any<CancellationToken>())
            .Returns(errorResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Something failed");
    }
}
