using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.FeatureFlags;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Endatix.Infrastructure.Tests.FeatureFlags;

public sealed class FeatureFlagsTargetingContextTests
{
    [Fact]
    public async Task GetContextAsync_ReturnsTenantGroup_WhenTenantIsSet()
    {
        // Arrange
        var accessor = new FeatureFlagsTargetingContext(
            new TestTenantContext(42),
            new TestUserContext("user-42"));

        // Act
        var context = await accessor.GetContextAsync();

        // Assert
        context.Groups.Should().ContainSingle("tenant-42");
        context.UserId.Should().Be("user-42");
    }

    [Fact]
    public async Task GetContextAsync_ReturnsEmptyGroups_WhenTenantIsNotSet()
    {
        // Arrange
        var accessor = new FeatureFlagsTargetingContext(
            new TestTenantContext(0),
            new TestUserContext("user-1"));

        // Act
        var context = await accessor.GetContextAsync();

        // Assert
        context.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task GetContextAsync_ReturnsAnonymousUserId_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var accessor = new FeatureFlagsTargetingContext(
            new TestTenantContext(1),
            new TestUserContext(userId: null));

        // Act
        var context = await accessor.GetContextAsync();

        // Assert
        context.UserId.Should().Be("anonymous");
    }

    private sealed class TestTenantContext(long tenantId) : ITenantContext
    {
        public long TenantId => tenantId;
    }

    private sealed class TestUserContext(string? userId) : IUserContext
    {
        public bool IsAnonymous => userId is null;
        public bool IsAuthenticated => userId is not null;
        public string? GetCurrentUserId() => userId;
        public User? GetCurrentUser() =>
            userId is null ? null : new User(1, userId, "test@example.com", true);
    }
}
