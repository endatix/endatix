using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class StandardSeedTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public StandardSeedTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Reset_database_without_standard_seed_keeps_database_empty()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);

        // Assert
        using var scope = _fixture.Factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var tenantCount = await appDb.Set<Tenant>().CountAsync(cancellationToken);
        var formCount = await appDb.Forms.CountAsync(cancellationToken);
        var roleCount = await identityDb.Roles.CountAsync(cancellationToken);
        var userCount = await identityDb.Users.CountAsync(cancellationToken);

        Assert.Equal(0, tenantCount);
        Assert.Equal(0, formCount);
        Assert.Equal(0, roleCount);
        Assert.Equal(0, userCount);
    }

    [Fact]
    public async Task Optional_standard_seed_populates_core_baseline_and_supports_post_seed_action()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var seedResult = await _fixture.ResetDatabaseAsync(
            useStandardSeed: true,
            afterSeed: static async (services, _, token) =>
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var firstTenantId = await db.Set<Tenant>()
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .FirstAsync(token);

                Form extraForm = new(firstTenantId, "custom-post-seed-form", isEnabled: true, isPublic: false);
                db.Forms.Add(extraForm);
                await db.SaveChangesAsync(token);
            },
            cancellationToken: cancellationToken);

        // Assert
        Assert.NotNull(seedResult);
        Assert.Equal(3, seedResult.TenantIds.Count);

        using var scope = _fixture.Factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        var tenantCount = await appDb.Set<Tenant>().CountAsync(cancellationToken);
        Assert.Equal(3, tenantCount);
        var persistedSystemRoleNames = SystemRole.AllSystemRoles
            .Where(x => x.IsPersisted)
            .Select(x => x.Name)
            .ToArray();

        foreach (var tenantId in seedResult.TenantIds)
        {
            var roleCount = await identityDb.Roles.CountAsync(
                x => x.TenantId == tenantId && persistedSystemRoleNames.Contains(x.Name!),
                cancellationToken);
            Assert.Equal(persistedSystemRoleNames.Length, roleCount);

            var hasAdminUser = await identityDb.Users.AnyAsync(
                x => x.TenantId == tenantId && x.UserName!.StartsWith("seed-admin-"),
                cancellationToken);
            Assert.True(hasAdminUser);

            var hasPublicForm = await appDb.Forms.AnyAsync(
                x => x.TenantId == tenantId && x.IsPublic,
                cancellationToken);
            var hasPrivateForm = await appDb.Forms.AnyAsync(
                x => x.TenantId == tenantId && !x.IsPublic,
                cancellationToken);

            Assert.True(hasPublicForm);
            Assert.True(hasPrivateForm);
        }

        var hasCustomForm = await appDb.Forms.AnyAsync(x => x.Name == "custom-post-seed-form", cancellationToken);
        Assert.True(hasCustomForm);
    }
}
