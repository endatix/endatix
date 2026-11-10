using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Authorization data carries the earliest <c>ExpiresAt</c> of the role permissions it holds, so the cache can stop
/// serving it when that permission lapses.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AuthorizationDataExpiryTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SEED_PASSWORD = "Password123!";
    private const string ROLE_NAME = "TemporaryRole";
    private const string USER_NAME = "temporary@example.com";

    [Fact]
    public async Task GetAuthorizationDataAsync_RolePermissionExpires_ExpiresAtIsTheEarliestExpiry()
    {
        // Arrange — two permissions expire, at +30 min and +10 min; one never does.
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SEED_PASSWORD }, ct);
        var tenantId = world.Tenants[0].Id;
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(tenantId, ROLE_NAME, ct);
        await builder.SeedRolePermissionsAsync(tenantId, ROLE_NAME, [Actions.Forms.View, Actions.Forms.Create, Actions.Forms.Edit], ct);
        await builder.SeedUserAsync(tenantId, USER_NAME, USER_NAME, password: SEED_PASSWORD, cancellationToken: ct);
        await builder.SeedUserRoleAssignmentAsync(tenantId, USER_NAME, ROLE_NAME, ct);
        var earliest = DateTime.UtcNow.AddMinutes(10);
        long userId;
        using (var scope = world.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            userId = await db.Users.Where(u => u.UserName == USER_NAME).Select(u => u.Id).SingleAsync(ct);
            var roleId = await db.Roles.Where(r => r.TenantId == tenantId && r.Name == ROLE_NAME).Select(r => r.Id).SingleAsync(ct);
            var grants = await db.RolePermissions.Include(rp => rp.Permission).Where(rp => rp.RoleId == roleId).ToListAsync(ct);
            grants.Single(rp => rp.Permission.Name == Actions.Forms.Create).SetExpiration(earliest.AddMinutes(20));
            grants.Single(rp => rp.Permission.Name == Actions.Forms.Edit).SetExpiration(earliest);
            await db.SaveChangesAsync(ct);
        }

        // Act
        using var readScope = world.Services.CreateScope();
        var provider = readScope.ServiceProvider.GetRequiredService<IAuthorizationDataProvider>();
        var result = await provider.GetAuthorizationDataAsync(userId, tenantId, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().Contain([Actions.Forms.View, Actions.Forms.Create, Actions.Forms.Edit]);
        result.Value.ExpiresAt.Should().BeCloseTo(earliest, TimeSpan.FromMilliseconds(1));
    }
}
