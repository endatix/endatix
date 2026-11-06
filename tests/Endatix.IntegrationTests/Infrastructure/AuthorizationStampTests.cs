using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authorization;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// <see cref="AppIdentityDbContext"/> changes the <see cref="AuthorizationStamp"/> in the same save as a change to
/// an authorization input, and leaves it alone otherwise.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class AuthorizationStampTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string RoleName = "StampedRole";
    private const string UserName = "stamped@example.com";

    [Fact]
    public async Task SaveChangesAsync_UserRoleAdded_ChangesStamp()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await PrepareAsync(ct);
        Guid before = await ReadStampAsync(world, ct);

        // Act
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedUserRoleAssignmentAsync(world.Tenants[0].Id, UserName, RoleName, ct);

        // Assert
        (await ReadStampAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChanges_RolePermissionRemoved_ChangesStamp()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await PrepareAsync(ct);
        Guid before = await ReadStampAsync(world, ct);

        // Act
        using (IServiceScope scope = world.Services.CreateScope())
        {
            AppIdentityDbContext db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var rolePermission = await db.RolePermissions.FirstAsync(ct);
            db.RolePermissions.Remove(rolePermission);
            db.SaveChanges();
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().NotBe(before);
    }

    [Fact]
    public async Task SaveChangesAsync_UserProfileChanged_KeepsStamp()
    {
        // Arrange
        CancellationToken ct = TestContext.Current.CancellationToken;
        IntegrationTestWorld world = await PrepareAsync(ct);
        Guid before = await ReadStampAsync(world, ct);

        // Act
        using (IServiceScope scope = world.Services.CreateScope())
        {
            UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            AppUser user = (await users.FindByNameAsync(UserName))!;
            user.PhoneNumber = "+10000000000";
            (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        }

        // Assert
        (await ReadStampAsync(world, ct)).Should().Be(before);
    }

    private async Task<IntegrationTestWorld> PrepareAsync(CancellationToken ct)
    {
        IntegrationTestWorld world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        long tenantId = world.Tenants[0].Id;
        IntegrationSeedBuilder builder = new(world.Services);
        await builder.SeedRoleAsync(tenantId, RoleName, ct);
        await builder.SeedRolePermissionsAsync(tenantId, RoleName, [Actions.Forms.View], ct);
        await builder.SeedUserAsync(tenantId, UserName, UserName, password: SeedPassword, cancellationToken: ct);
        return world;
    }

    private static async Task<Guid> ReadStampAsync(IntegrationTestWorld world, CancellationToken ct)
    {
        using IServiceScope scope = world.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthorizationStampReader>().ReadAsync(ct);
    }
}
