using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class ListPlatformAdminsTests
{
    private readonly IPlatformAdminUserListing _listing = Substitute.For<IPlatformAdminUserListing>();

    [Fact]
    public async Task ExecuteAsync_WhenPlatformAdminRoleMissing_ReturnsEmptyPageWithoutListingUsers()
    {
        // Arrange
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns((long?)null);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            page: 1,
            pageSize: 10,
            search: null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRecords.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        await _listing.DidNotReceive().ListAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string?>(),
            Arg.Any<long?>(),
            Arg.Any<PlatformAdminUserScopeFilter>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlatformAdminRoleExists_ListsUsersWithLocalRoleScope()
    {
        // Arrange
        const long roleId = 42;
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(10));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns(roleId);
        _listing.ListAsync(
                1,
                10,
                "admin",
                roleId,
                PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole,
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            page: 1,
            pageSize: 10,
            search: "admin",
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
        await _listing.Received(1).ListAsync(
            1,
            10,
            "admin",
            roleId,
            PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlatformAdminRoleMissing_NormalizesEmptyPageSize()
    {
        // Arrange
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns((long?)null);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            page: 1,
            pageSize: 0,
            search: null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(1);
    }
}
