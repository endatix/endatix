using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdminCandidates;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class ListPlatformAdminCandidatesTests
{
    private readonly IPlatformAdminUserListing _listing = Substitute.For<IPlatformAdminUserListing>();

    [Fact]
    public async Task ExecuteAsync_WhenPlatformAdminRoleMissing_ListsAllUsersWithoutRoleFilter()
    {
        // Arrange
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(10));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns((long?)null);
        _listing.ListAsync(
                1,
                10,
                null,
                null,
                PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole,
                Arg.Any<CancellationToken>(),
                prioritizeExternalPlatformAdminRole: true)
            .Returns(expected);
        var sut = new ListPlatformAdminCandidates(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            page: 1,
            pageSize: 10,
            search: null,
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
        await _listing.Received(1).ListAsync(
            1,
            10,
            null,
            null,
            PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole,
            Arg.Any<CancellationToken>(),
            prioritizeExternalPlatformAdminRole: true);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlatformAdminRoleExists_ExcludesUsersWithLocalRole()
    {
        // Arrange
        const long roleId = 7;
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(10));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns(roleId);
        _listing.ListAsync(
                2,
                25,
                "nominee",
                roleId,
                PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole,
                Arg.Any<CancellationToken>(),
                prioritizeExternalPlatformAdminRole: true)
            .Returns(expected);
        var sut = new ListPlatformAdminCandidates(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            page: 2,
            pageSize: 25,
            search: "nominee",
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
        await _listing.Received(1).ListAsync(
            2,
            25,
            "nominee",
            roleId,
            PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole,
            Arg.Any<CancellationToken>(),
            prioritizeExternalPlatformAdminRole: true);
    }
}
