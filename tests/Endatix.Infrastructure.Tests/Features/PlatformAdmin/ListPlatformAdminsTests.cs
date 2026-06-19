using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.PlatformAdmin.Common;
using Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformAdmins;

namespace Endatix.Infrastructure.Tests.Features.PlatformAdmin;

public sealed class ListPlatformAdminsTests
{
    private readonly IPlatformAdminUserListing _listing = Substitute.For<IPlatformAdminUserListing>();

    [Fact]
    public async Task ExecuteAsync_WhenApprovedScopeAndRoleMissing_ReturnsEmptyPageWithoutListingUsers()
    {
        // Arrange
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns((long?)null);
        var sut = new ListPlatformAdmins(_listing);
        var paging = new SearchablePageRequest(1, 10, null);

        // Act
        var result = await sut.ExecuteAsync(
            paging,
            PlatformAdminListScope.Approved,
            tenantId: null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRecords.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        await _listing.DidNotReceive().ListAsync(
            Arg.Any<SearchablePageRequest>(),
            Arg.Any<PlatformAdminUserListCriteria>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenApprovedScopeAndRoleExists_ListsUsersWithLocalRoleScope()
    {
        // Arrange
        const long roleId = 42;
        var paging = new SearchablePageRequest(1, 10, "admin");
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(10));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns(roleId);
        _listing.ListAsync(
                paging,
                Arg.Is<PlatformAdminUserListCriteria>(criteria =>
                    criteria.PlatformAdminRoleId == roleId &&
                    criteria.ScopeFilter == PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            paging,
            PlatformAdminListScope.Approved,
            tenantId: null,
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllScope_ListsUsersWithoutRoleFilter()
    {
        // Arrange
        const long roleId = 42;
        var paging = new SearchablePageRequest(1, 10, null);
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(10));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns(roleId);
        _listing.ListAsync(
                paging,
                Arg.Is<PlatformAdminUserListCriteria>(criteria =>
                    criteria.PlatformAdminRoleId == roleId &&
                    criteria.ScopeFilter == PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole &&
                    criteria.TenantId == 5 &&
                    criteria.PrioritizeLocalPlatformAdminRole),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            paging,
            PlatformAdminListScope.All,
            tenantId: 5,
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
        await _listing.Received(1).ListAsync(
            paging,
            Arg.Is<PlatformAdminUserListCriteria>(criteria =>
                criteria.PlatformAdminRoleId == roleId &&
                criteria.ScopeFilter == PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole &&
                criteria.TenantId == 5 &&
                !criteria.PrioritizeExternalPlatformAdminRole &&
                criteria.PrioritizeLocalPlatformAdminRole),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidatesScope_ExcludesUsersWithLocalRole()
    {
        // Arrange
        const long roleId = 7;
        var paging = new SearchablePageRequest(2, 25, "nominee");
        var expected = Result.Success(
            Paged<PlatformAdminUserListItem>.Empty(25));
        _listing.GetPlatformAdminRoleIdAsync(Arg.Any<CancellationToken>())
            .Returns(roleId);
        _listing.ListAsync(
                paging,
                Arg.Is<PlatformAdminUserListCriteria>(criteria =>
                    criteria.PlatformAdminRoleId == roleId &&
                    criteria.ScopeFilter == PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole &&
                    criteria.PrioritizeExternalPlatformAdminRole),
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var sut = new ListPlatformAdmins(_listing);

        // Act
        var result = await sut.ExecuteAsync(
            paging,
            PlatformAdminListScope.Candidates,
            tenantId: null,
            CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(PlatformAdminListScope.Approved, PlatformAdminUserScopeFilter.MustHaveLocalPlatformAdminRole, false, false)]
    [InlineData(PlatformAdminListScope.Candidates, PlatformAdminUserScopeFilter.MustNotHaveLocalPlatformAdminRole, true, false)]
    [InlineData(PlatformAdminListScope.All, PlatformAdminUserScopeFilter.IgnoreLocalPlatformAdminRole, false, true)]
    public void ResolveScopeFilter_WhenRoleExists_MapsExpectedScope(
        PlatformAdminListScope scope,
        PlatformAdminUserScopeFilter expectedFilter,
        bool expectedPrioritizeExternal,
        bool expectedPrioritizeLocal)
    {
        var (scopeFilter, prioritizeExternal, prioritizeLocal) =
            ListPlatformAdmins.ResolveScopeFilter(scope, platformAdminRoleId: 1);

        scopeFilter.Should().Be(expectedFilter);
        prioritizeExternal.Should().Be(expectedPrioritizeExternal);
        prioritizeLocal.Should().Be(expectedPrioritizeLocal);
    }
}
