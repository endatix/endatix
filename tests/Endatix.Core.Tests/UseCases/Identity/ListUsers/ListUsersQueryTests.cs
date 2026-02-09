using Endatix.Core.UseCases.Identity.ListUsers;

namespace Endatix.Core.Tests.UseCases.Identity.ListUsers;

public class ListUsersQueryTests
{
    [Fact]
    public void Constructor_SetsPageAndPageSize()
    {
        var query = new ListUsersQuery(Page: 2, PageSize: 20);

        query.Page.Should().Be(2);
        query.PageSize.Should().Be(20);
    }

    [Fact]
    public void Constructor_AllowsNullPageAndPageSize()
    {
        var query = new ListUsersQuery(null, null);

        query.Page.Should().BeNull();
        query.PageSize.Should().BeNull();
    }
}
