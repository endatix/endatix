using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

[Collection(nameof(OssIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class StandardSeedTests
{
    private readonly OssIntegrationWebHostFixture _fixture;

    public StandardSeedTests(OssIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Reset_database_without_standard_seed_keeps_database_empty()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await _fixture.ResetDatabaseAsync(cancellationToken: cancellationToken);

        using var scope = _fixture.Factory.Services.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        int tenantCount = await appDb.Set<Tenant>().CountAsync(cancellationToken);
        int formCount = await appDb.Forms.CountAsync(cancellationToken);
        int roleCount = await identityDb.Roles.CountAsync(cancellationToken);
        int userCount = await identityDb.Users.CountAsync(cancellationToken);

        Assert.Equal(0, tenantCount);
        Assert.Equal(0, formCount);
        Assert.Equal(0, roleCount);
        Assert.Equal(0, userCount);
    }

    [Fact]
    public async Task Optional_standard_seed_populates_core_baseline_and_supports_post_seed_action()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var seedResult = await _fixture.ResetDatabaseAsync(
            useStandardSeed: true,
            afterSeed: static async (services, _, token) =>
            {
                using var scope = services.CreateScope();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                long firstTenantId = await db.Set<Tenant>()
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .FirstAsync(token);

                Form extraForm = new(firstTenantId, "custom-post-seed-form", isEnabled: true, isPublic: false);
                db.Forms.Add(extraForm);
                await db.SaveChangesAsync(token);
            },
            cancellationToken: cancellationToken);

        Assert.NotNull(seedResult);
        Assert.Equal(3, seedResult.TenantIds.Count);

        using var scope = _fixture.Factory.Services.CreateScope();
        AppDbContext appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        int tenantCount = await appDb.Set<Tenant>().CountAsync(cancellationToken);
        Assert.Equal(3, tenantCount);
        var persistedSystemRoleNames = SystemRole.AllSystemRoles
            .Where(x => x.IsPersisted)
            .Select(x => x.Name)
            .ToArray();

        foreach (var tenantId in seedResult.TenantIds)
        {
            int roleCount = await identityDb.Roles.CountAsync(
                x => x.TenantId == tenantId && persistedSystemRoleNames.Contains(x.Name!),
                cancellationToken);
            Assert.Equal(3, roleCount);

            bool hasAdminUser = await identityDb.Users.AnyAsync(
                x => x.TenantId == tenantId && x.UserName!.StartsWith("seed-admin-"),
                cancellationToken);
            Assert.True(hasAdminUser);

            bool hasPublicForm = await appDb.Forms.AnyAsync(
                x => x.TenantId == tenantId && x.IsPublic,
                cancellationToken);
            bool hasPrivateForm = await appDb.Forms.AnyAsync(
                x => x.TenantId == tenantId && !x.IsPublic,
                cancellationToken);

            Assert.True(hasPublicForm);
            Assert.True(hasPrivateForm);
        }

        bool hasCustomForm = await appDb.Forms.AnyAsync(x => x.Name == "custom-post-seed-form", cancellationToken);
        Assert.True(hasCustomForm);
    }
}
