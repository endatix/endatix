using Endatix.Core.UseCases.Identity.ListUsers;

namespace Endatix.Core.Tests.UseCases.Identity.ListUsers;

public class ListUsersQueryTests
{
    [Fact]
    public void Constructor_NormalizesPagingAndFilters()
    {
        var query = new ListUsersQuery(
            page: 2,
            pageSize: 20,
            search: " alice ",
            role: " Admin ",
            status: "PENDING");

        query.Page.Should().Be(2);
        query.PageSize.Should().Be(20);
        query.Skip.Should().Be(20);
        query.Search.Should().Be("alice");
        query.Role.Should().Be("Admin");
        query.Status.Should().Be("pending");
    }

    [Fact]
    public void Constructor_DefaultsNullPagingAndFilters()
    {
        var query = new ListUsersQuery(null, null, null, null, null);

        query.Page.Should().Be(ListUsersQuery.DefaultPage);
        query.PageSize.Should().Be(ListUsersQuery.DefaultPageSize);
        query.Skip.Should().Be(0);
        query.Search.Should().BeNull();
        query.Role.Should().BeNull();
        query.Status.Should().BeNull();
    }

    [Fact]
    public void Constructor_ClampsPageSizeToMaximum()
    {
        var query = new ListUsersQuery(1, 1_000, null, null, null);

        query.PageSize.Should().Be(ListUsersQuery.MaxPageSize);
    }
}
