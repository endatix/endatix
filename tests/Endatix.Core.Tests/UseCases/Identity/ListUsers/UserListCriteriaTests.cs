using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.UseCases.Identity.ListUsers;

namespace Endatix.Core.Tests.UseCases.Identity.ListUsers;

/// <summary>
/// Filter normalization moved off <c>ListUsersQuery</c> onto the criteria value object,
/// so it is guaranteed for every caller of <c>IUserService.ListUsersAsync</c>.
/// Paging/search normalization is covered by PageRequestTests / SearchablePageRequestTests.
/// </summary>
public class UserListCriteriaTests
{
    [Fact]
    public void Constructor_TrimsRoleAndLowercasesStatus()
    {
        var criteria = new UserListCriteria(role: " Admin ", status: "PENDING");

        criteria.Role.Should().Be("Admin");
        criteria.Status.Should().Be("pending");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NormalizesBlankFiltersToNull(string? value)
    {
        var criteria = new UserListCriteria(role: value, status: value);

        criteria.Role.Should().BeNull();
        criteria.Status.Should().BeNull();
    }

    [Fact]
    public void Constructor_DefaultsToNoSortAndNoDateBounds()
    {
        var criteria = new UserListCriteria();

        criteria.Sort.Should().BeNull();
        criteria.LastLogin.HasBounds.Should().BeFalse();
    }

    [Fact]
    public void Constructor_KeepsSortAndLastLoginBounds()
    {
        var sort = new SortRequest<UserListSortBy>(UserListSortBy.LastLoginAt, SortDirection.Desc);
        var lastLogin = new UtcDateTimeRange(
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var criteria = new UserListCriteria(sort: sort, lastLogin: lastLogin);

        criteria.Sort!.Field.Should().Be(UserListSortBy.LastLoginAt);
        criteria.Sort.IsDescending.Should().BeTrue();
        criteria.LastLogin.Should().Be(lastLogin);
    }
}
